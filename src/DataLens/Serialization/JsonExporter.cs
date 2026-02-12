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
                row.Add(reader.GetDouble());
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
                if (double.IsNaN(v) || double.IsInfinity(v))
                    writer.WriteNullValue();
                else
                    writer.WriteNumberValue(Math.Round(v, 6));
            }
            writer.WriteEndArray();
        }
        writer.WriteEndArray();
    }
}
