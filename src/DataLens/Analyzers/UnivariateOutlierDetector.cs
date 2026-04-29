using DataLens.Models;

namespace DataLens.Analyzers;

/// <summary>
/// 단변량 (per-column) 이상치 검출기. Tukey IQR / 3σ / Hampel.
/// </summary>
/// <remarks>
/// <para>
/// UInsight C# 바인딩은 0.9.1 시점 단변량 outlier API 를 노출하지 않으므로 DataLens 가
/// 직접 구현한다. Rust 측 <c>OutlierMethod</c> enum (Iqr / Zscore / ModifiedZscore) 와
/// 알고리즘 정의 동치 — fence/center/spread/method 명명도 u-insight closure 노트와 일치.
/// </para>
/// <para>
/// 비용: 컬럼당 O(n log n) (IQR/Hampel 의 percentile/median 정렬). multivariate IF/LOF/Mahalanobis
/// 와 직교 — 두 시각이 함께 보강한다.
/// </para>
/// </remarks>
public static class UnivariateOutlierDetector
{
    /// <summary>
    /// Tukey IQR fence: <c>Q1 - k·IQR</c> ~ <c>Q3 + k·IQR</c>. k=1.5 (mild), k=3.0 (extreme).
    /// </summary>
    public static UnivariateOutlierResult Tukey(double[] values, double k = 1.5)
    {
        var clean = CleanFinite(values);
        if (clean.Length < 4) return Empty(UnivariateOutlierMethod.Tukey);

        Array.Sort(clean);
        double q1 = Quantile(clean, 0.25);
        double q3 = Quantile(clean, 0.75);
        double iqr = q3 - q1;
        double median = Quantile(clean, 0.5);
        double lower = q1 - k * iqr;
        double upper = q3 + k * iqr;

        return BuildResult(
            method: UnivariateOutlierMethod.Tukey,
            values: values,
            lower: lower,
            upper: upper,
            center: median,
            spread: iqr);
    }

    /// <summary>
    /// 3-Sigma rule: <c>mean ± k·std</c>. k=3.0 default. 정규분포 가정 — 이상치에 robust 하지 않음.
    /// </summary>
    public static UnivariateOutlierResult ThreeSigma(double[] values, double k = 3.0)
    {
        var clean = CleanFinite(values);
        if (clean.Length < 2) return Empty(UnivariateOutlierMethod.ThreeSigma);

        double mean = 0;
        for (int i = 0; i < clean.Length; i++) mean += clean[i];
        mean /= clean.Length;

        double ssq = 0;
        for (int i = 0; i < clean.Length; i++)
        {
            var d = clean[i] - mean;
            ssq += d * d;
        }
        double std = Math.Sqrt(ssq / (clean.Length - 1));

        double lower = mean - k * std;
        double upper = mean + k * std;

        return BuildResult(
            method: UnivariateOutlierMethod.ThreeSigma,
            values: values,
            lower: lower,
            upper: upper,
            center: mean,
            spread: std);
    }

    /// <summary>
    /// Hampel identifier: <c>median ± k · 1.4826 · MAD</c>. k=3.0 default.
    /// 정규분포에서 MAD 가 σ 의 unbiased estimator 가 되도록 1.4826 = 1/Φ⁻¹(0.75) scale 보정.
    /// 이상치에 가장 robust.
    /// </summary>
    public static UnivariateOutlierResult Hampel(double[] values, double k = 3.0)
    {
        var clean = CleanFinite(values);
        if (clean.Length < 4) return Empty(UnivariateOutlierMethod.Hampel);

        Array.Sort(clean);
        double median = Quantile(clean, 0.5);

        var deviations = new double[clean.Length];
        for (int i = 0; i < clean.Length; i++) deviations[i] = Math.Abs(clean[i] - median);
        Array.Sort(deviations);
        double mad = Quantile(deviations, 0.5);
        double scaledMad = 1.4826 * mad;

        double lower = median - k * scaledMad;
        double upper = median + k * scaledMad;

        return BuildResult(
            method: UnivariateOutlierMethod.Hampel,
            values: values,
            lower: lower,
            upper: upper,
            center: median,
            spread: scaledMad);
    }

    private static UnivariateOutlierResult BuildResult(
        UnivariateOutlierMethod method,
        double[] values,
        double lower,
        double upper,
        double center,
        double spread)
    {
        var anomalyIndices = new List<int>();
        for (int i = 0; i < values.Length; i++)
        {
            var v = values[i];
            if (!IsFinite(v)) continue;
            if (v < lower || v > upper) anomalyIndices.Add(i);
        }
        return new UnivariateOutlierResult
        {
            Method = method,
            LowerFence = lower,
            UpperFence = upper,
            Center = center,
            Spread = spread,
            AnomalyIndices = anomalyIndices,
            AnomalyCount = anomalyIndices.Count
        };
    }

    private static UnivariateOutlierResult Empty(UnivariateOutlierMethod method) => new()
    {
        Method = method,
        AnomalyIndices = []
    };

    private static double[] CleanFinite(double[] values)
    {
        int count = 0;
        for (int i = 0; i < values.Length; i++) if (IsFinite(values[i])) count++;

        var clean = new double[count];
        int j = 0;
        for (int i = 0; i < values.Length; i++) if (IsFinite(values[i])) clean[j++] = values[i];
        return clean;
    }

    private static bool IsFinite(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

    /// <summary>
    /// 정렬된 배열에서 quantile 계산 (linear interpolation, R-7 / numpy default).
    /// </summary>
    private static double Quantile(double[] sorted, double q)
    {
        if (sorted.Length == 0) return double.NaN;
        if (sorted.Length == 1) return sorted[0];

        double pos = q * (sorted.Length - 1);
        int lo = (int)Math.Floor(pos);
        int hi = (int)Math.Ceiling(pos);
        if (lo == hi) return sorted[lo];
        double frac = pos - lo;
        return sorted[lo] + frac * (sorted[hi] - sorted[lo]);
    }
}
