using System.Text.Json;
using DataLens.Models;
using DataLens.Serialization;

namespace DataLens.Tests;

public class JsonNanInfinityTests
{
    [Fact]
    public void Serialize_DoubleNaN_UsesNamedLiteral()
    {
        var report = new IsolationForestReport
        {
            Scores = new[] { 1.0, double.NaN, double.PositiveInfinity, double.NegativeInfinity },
            Anomalies = new[] { false, true, true, true },
            AnomalyCount = 3,
            Threshold = double.NaN
        };

        var json = JsonExporter.Serialize(report);

        Assert.Contains("\"NaN\"", json);
        Assert.Contains("\"Infinity\"", json);
        Assert.Contains("\"-Infinity\"", json);
    }

    [Fact]
    public void Roundtrip_DoubleArrayMatrix_PreservesNaNAndInfinity()
    {
        var matrix = new double[,]
        {
            { 1.0, double.NaN },
            { double.PositiveInfinity, double.NegativeInfinity },
            { 2.5, 3.7 }
        };

        var holder = new MatrixHolder { M = matrix };
        var json = JsonExporter.Serialize(holder);

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowNamedFloatingPointLiterals,
            Converters = { new DoubleArrayMatrixConverter() }
        };
        var restored = JsonSerializer.Deserialize<MatrixHolder>(json, options);

        Assert.NotNull(restored?.M);
        Assert.True(double.IsNaN(restored!.M![0, 1]));
        Assert.True(double.IsPositiveInfinity(restored.M[1, 0]));
        Assert.True(double.IsNegativeInfinity(restored.M[1, 1]));
        Assert.Equal(1.0, restored.M[0, 0]);
        Assert.Equal(2.5, restored.M[2, 0]);
        Assert.Equal(3.7, restored.M[2, 1]);
    }

    [Fact]
    public void AnalysisResult_ToJson_WithDegenerateScores_DoesNotThrow()
    {
        var result = new AnalysisResult
        {
            Outliers = new OutlierReport
            {
                IsolationForest = new IsolationForestReport
                {
                    Scores = new[] { double.NaN, double.PositiveInfinity, 0.5 },
                    Anomalies = new[] { true, true, false },
                    AnomalyCount = 2,
                    Threshold = double.NaN
                },
                TotalRows = 3,
                OutlierCount = 2,
                OutlierPercentage = 66.7
            }
        };

        var json = result.ToJson(Section.Outliers);
        Assert.Contains("\"NaN\"", json);
    }

    private class MatrixHolder
    {
        public double[,]? M { get; set; }
    }
}
