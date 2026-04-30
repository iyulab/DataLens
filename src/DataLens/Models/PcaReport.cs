namespace DataLens.Models;

public class PcaReport
{
    /// <summary>분산 임계(<see cref="AnalysisOptions.PcaVarianceThreshold"/>) 누적 도달까지의 성분 수.</summary>
    public uint NComponents { get; init; }

    /// <summary>입력 행렬의 원본 변수(컬럼) 수 — <see cref="Loadings"/> 의 두 번째 차원과 일치.</summary>
    public uint NFeatures { get; init; }

    /// <summary>입력 행렬의 샘플(행) 수 — <see cref="Scores"/> 의 첫 번째 길이와 일치.</summary>
    public uint NSamples { get; init; }

    /// <summary>성분별 설명 분산 비율 (length = 산출 성분 수, 보통 ≥ <see cref="NComponents"/>).</summary>
    public double[] ExplainedVariance { get; init; } = [];

    /// <summary>누적 설명 분산 비율 (length = <see cref="ExplainedVariance"/>).</summary>
    public double[] CumulativeVariance { get; init; } = [];

    /// <summary><see cref="NComponents"/> 까지의 누적 설명 분산 합.</summary>
    public double TotalExplainedVariance { get; init; }

    /// <summary>
    /// 성분 적재량 (component loadings), shape <c>[Length, <see cref="NFeatures"/>]</c>.
    /// 행 k 는 PC{k+1} 의 단위 노름 가중치 — <c>Loadings[k, :]</c> 가 성분 k 의 eigenvector.
    /// 변수 j 의 PC{k+1} 기여도 해석에 사용. <c>null</c> 이면 미산출.
    /// </summary>
    public double[,]? Loadings { get; init; }

    /// <summary>
    /// PC 공간 사영 좌표 (scores), shape <c>[<see cref="NSamples"/>][NComponents]</c>.
    /// 행 i 는 입력 샘플 i 의 PC 좌표. PC1×PC2 산점도, biplot, 클러스터 색칠 시각화에 사용.
    /// </summary>
    public double[][] Scores { get; init; } = [];
}
