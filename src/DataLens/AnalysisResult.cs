using System.Text.Json;
using System.Text.Json.Serialization;
using DataLens.Models;
using DataLens.Serialization;

namespace DataLens;

public enum Section
{
    Profile,
    Descriptive,
    Correlation,
    Regression,
    Clusters,
    Outliers,
    Distribution,
    Features,
    Pca
}

/// <summary>
/// 전체 분석 결과 컨테이너.
/// </summary>
public class AnalysisResult
{
    public ProfileReport? Profile { get; init; }
    public DescriptiveReport? Descriptive { get; init; }
    public CorrelationReport? Correlation { get; init; }
    public RegressionReport? Regression { get; init; }
    public ClusterReport? Clusters { get; init; }
    public OutlierReport? Outliers { get; init; }
    public DistributionReport? Distribution { get; init; }
    public FeatureReport? Features { get; init; }
    public PcaReport? Pca { get; init; }

    /// <summary>
    /// 분석 중 발생한 비치명적 경고 목록.
    /// null인 분석 필드가 있다면 여기서 원인을 확인할 수 있다.
    /// </summary>
    public IReadOnlyList<AnalysisWarning> Warnings { get; init; } = [];

    public string ToJson(JsonSerializerOptions? options = null)
    {
        return JsonExporter.Serialize(this, options);
    }

    public string ToJson(Section section, JsonSerializerOptions? options = null)
    {
        object? target = section switch
        {
            Section.Profile => Profile,
            Section.Descriptive => Descriptive,
            Section.Correlation => Correlation,
            Section.Regression => Regression,
            Section.Clusters => Clusters,
            Section.Outliers => Outliers,
            Section.Distribution => Distribution,
            Section.Features => Features,
            Section.Pca => Pca,
            _ => throw new ArgumentOutOfRangeException(nameof(section))
        };

        return JsonExporter.Serialize(target, options);
    }

    public async Task ToJsonAsync(string filePath, JsonSerializerOptions? options = null)
    {
        var json = ToJson(options);
        await File.WriteAllTextAsync(filePath, json);
    }
}
