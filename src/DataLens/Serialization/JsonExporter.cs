using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DataLens.Serialization;

public static class JsonExporter
{
    private static readonly JsonSerializerOptions DefaultOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // LOF / Mahalanobis / IsolationForest 등은 degenerate input 에서 정상적으로 NaN/±Inf 를 산출한다.
        // 의미 보존을 위해 named literal 로 직렬화 (round-trip 가능).
        NumberHandling = JsonNumberHandling.AllowNamedFloatingPointLiterals,
        Converters = { new DoubleArrayMatrixConverter() }
    };

    public static string Serialize<T>(T value, JsonSerializerOptions? options = null)
    {
        return JsonSerializer.Serialize(value, options ?? DefaultOptions);
    }
}

/// <summary>
/// double[,] 2D 배열을 JSON 2D 배열로 직렬화하는 컨버터.
/// System.Text.Json은 기본적으로 다차원 배열을 지원하지 않으므로 필요하다.
/// NaN/±Infinity 는 named literal ("NaN" / "Infinity" / "-Infinity") 로 출력하여
/// 일반 double 필드와 동일한 정책 (NumberHandling.AllowNamedFloatingPointLiterals) 을 따른다.
/// </summary>
public class DoubleArrayMatrixConverter : JsonConverter<double[,]>
{
    public override double[,]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        var rows = new List<List<double>>();
        reader.Read(); // StartArray (outer)

        while (reader.TokenType != JsonTokenType.EndArray)
        {
            var row = new List<double>();
            reader.Read(); // StartArray (inner)
            while (reader.TokenType != JsonTokenType.EndArray)
            {
                row.Add(ReadCell(ref reader));
                reader.Read();
            }
            rows.Add(row);
            reader.Read(); // next row or EndArray (outer)
        }

        if (rows.Count == 0) return new double[0, 0];

        int nCols = rows[0].Count;
        var result = new double[rows.Count, nCols];
        for (int i = 0; i < rows.Count; i++)
            for (int j = 0; j < nCols; j++)
                result[i, j] = rows[i][j];

        return result;
    }

    private static double ReadCell(ref Utf8JsonReader reader)
    {
        return reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetDouble(),
            JsonTokenType.String => ParseNamedLiteral(reader.GetString()),
            JsonTokenType.Null => double.NaN,
            _ => throw new JsonException($"Unexpected token in matrix cell: {reader.TokenType}")
        };
    }

    private static double ParseNamedLiteral(string? s) => s switch
    {
        "NaN" => double.NaN,
        "Infinity" => double.PositiveInfinity,
        "-Infinity" => double.NegativeInfinity,
        _ => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)
            ? d
            : throw new JsonException($"Cannot parse matrix cell: '{s}'")
    };

    public override void Write(Utf8JsonWriter writer, double[,] value, JsonSerializerOptions options)
    {
        int rows = value.GetLength(0);
        int cols = value.GetLength(1);

        writer.WriteStartArray();
        for (int i = 0; i < rows; i++)
        {
            writer.WriteStartArray();
            for (int j = 0; j < cols; j++)
            {
                var v = value[i, j];
                if (double.IsNaN(v))
                    writer.WriteStringValue("NaN");
                else if (double.IsPositiveInfinity(v))
                    writer.WriteStringValue("Infinity");
                else if (double.IsNegativeInfinity(v))
                    writer.WriteStringValue("-Infinity");
                else
                    writer.WriteNumberValue(Math.Round(v, 6));
            }
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }
}
