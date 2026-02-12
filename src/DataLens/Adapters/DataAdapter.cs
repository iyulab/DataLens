using FilePrepper.Pipeline;

namespace DataLens.Adapters;

/// <summary>
/// FilePrepper DataFrame → UInsight 입력 형식 변환 어댑터.
/// DataFrame의 문자열 데이터를 숫자 행렬로 변환한다.
/// </summary>
public class DataAdapter
{
    private readonly DataFrame _dataFrame;
    private readonly List<string> _numericColumns = [];
    private readonly List<string> _categoricalColumns = [];

    public IReadOnlyList<string> NumericColumns => _numericColumns;
    public IReadOnlyList<string> CategoricalColumns => _categoricalColumns;
    public IReadOnlyList<string> AllColumns => _dataFrame.ColumnNames;
    public DataFrame DataFrame => _dataFrame;
    public int RowCount => _dataFrame.RowCount;
    public int ColumnCount => _dataFrame.ColumnCount;

    public DataAdapter(DataFrame dataFrame)
    {
        _dataFrame = dataFrame ?? throw new ArgumentNullException(nameof(dataFrame));
        ClassifyColumns();
    }

    private void ClassifyColumns()
    {
        foreach (var col in _dataFrame.ColumnNames)
        {
            var values = _dataFrame.GetColumn(col);
            int numericCount = 0;
            int totalNonEmpty = 0;

            foreach (var val in values)
            {
                if (string.IsNullOrWhiteSpace(val)) continue;
                totalNonEmpty++;
                if (double.TryParse(val, out _))
                    numericCount++;
            }

            // 80% 이상이 숫자이면 숫자 컬럼으로 분류
            if (totalNonEmpty > 0 && (double)numericCount / totalNonEmpty >= 0.8)
                _numericColumns.Add(col);
            else
                _categoricalColumns.Add(col);
        }
    }

    /// <summary>
    /// 지정 컬럼들을 double[,] 행렬로 변환. 컬럼 미지정 시 모든 숫자 컬럼 사용.
    /// 파싱 실패 또는 결측값은 NaN으로 채워진다.
    /// </summary>
    public double[,] ToNumericMatrix(params string[] columns)
    {
        var cols = columns.Length > 0 ? columns : _numericColumns.ToArray();
        if (cols.Length == 0)
            throw new InvalidOperationException("No numeric columns available.");

        var rows = _dataFrame.Rows;
        var matrix = new double[rows.Count, cols.Length];

        for (int j = 0; j < cols.Length; j++)
        {
            int i = 0;
            foreach (var row in rows)
            {
                row.TryGetValue(cols[j], out var val);
                matrix[i, j] = double.TryParse(val, out var d) ? d : double.NaN;
                i++;
            }
        }

        return matrix;
    }

    /// <summary>
    /// 단일 컬럼 → double[]. 결측값은 NaN.
    /// </summary>
    public double[] ToArray(string column)
    {
        var values = _dataFrame.GetColumn(column);
        return values.Select(v => double.TryParse(v, out var d) ? d : double.NaN).ToArray();
    }

    /// <summary>
    /// 범주형 컬럼 → uint[] (label encoding).
    /// 고유값을 정렬 후 0부터 인덱싱한다.
    /// </summary>
    public uint[] ToLabels(string column)
    {
        var values = _dataFrame.GetColumn(column).ToList();
        var uniqueValues = values.Where(v => !string.IsNullOrWhiteSpace(v))
                                  .Distinct()
                                  .OrderBy(v => v)
                                  .ToList();
        var labelMap = new Dictionary<string, uint>();
        for (int i = 0; i < uniqueValues.Count; i++)
            labelMap[uniqueValues[i]] = (uint)i;

        return values.Select(v =>
            string.IsNullOrWhiteSpace(v) ? uint.MaxValue : labelMap[v]
        ).ToArray();
    }

    /// <summary>
    /// NaN이 포함된 행을 제거한 clean matrix 반환.
    /// </summary>
    public double[,] ToCleanMatrix(params string[] columns)
    {
        var cols = columns.Length > 0 ? columns : _numericColumns.ToArray();
        if (cols.Length == 0)
            throw new InvalidOperationException("No numeric columns available.");

        var rows = _dataFrame.Rows;
        var cleanRows = new List<double[]>();

        foreach (var row in rows)
        {
            var values = new double[cols.Length];
            bool hasNaN = false;
            for (int j = 0; j < cols.Length; j++)
            {
                row.TryGetValue(cols[j], out var val);
                if (double.TryParse(val, out var d))
                    values[j] = d;
                else
                {
                    hasNaN = true;
                    break;
                }
            }
            if (!hasNaN)
                cleanRows.Add(values);
        }

        var matrix = new double[cleanRows.Count, cols.Length];
        for (int i = 0; i < cleanRows.Count; i++)
            for (int j = 0; j < cols.Length; j++)
                matrix[i, j] = cleanRows[i][j];

        return matrix;
    }

    /// <summary>
    /// NaN이 포함된 값을 제거한 clean array 반환.
    /// </summary>
    public double[] ToCleanArray(string column)
    {
        return ToArray(column).Where(v => !double.IsNaN(v)).ToArray();
    }

    /// <summary>
    /// DataFrame을 CSV 문자열로 변환 (UInsight ProfileCsv용).
    /// </summary>
    public string ToCsvString()
    {
        return _dataFrame.ToCsv();
    }
}
