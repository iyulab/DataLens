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

    /// <summary>
    /// 결측값을 중앙값으로 대체한 matrix 반환.
    /// 클러스터링, PCA, 이상치 탐지 등 모든 행이 필요한 분석에 사용.
    /// </summary>
    public double[,] ToImputedMatrix(params string[] columns)
    {
        var cols = columns.Length > 0 ? columns : _numericColumns.ToArray();
        if (cols.Length == 0)
            throw new InvalidOperationException("No numeric columns available.");

        // 먼저 원본 matrix (NaN 포함) 생성
        var rows = _dataFrame.Rows;
        int nRows = rows.Count;
        var matrix = new double[nRows, cols.Length];

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

        // 컬럼별 중앙값으로 NaN 대체
        for (int j = 0; j < cols.Length; j++)
        {
            var validValues = new List<double>();
            for (int i = 0; i < nRows; i++)
            {
                if (!double.IsNaN(matrix[i, j]))
                    validValues.Add(matrix[i, j]);
            }

            if (validValues.Count == 0) continue;

            validValues.Sort();
            double median = validValues.Count % 2 == 0
                ? (validValues[validValues.Count / 2 - 1] + validValues[validValues.Count / 2]) / 2.0
                : validValues[validValues.Count / 2];

            for (int i = 0; i < nRows; i++)
            {
                if (double.IsNaN(matrix[i, j]))
                    matrix[i, j] = median;
            }
        }

        return matrix;
    }

    /// <summary>
    /// 결측값 대체 + Z-Score 정규화된 matrix 반환.
    /// 클러스터링, PCA 등 스케일에 민감한 분석에 사용.
    /// </summary>
    public double[,] ToScaledMatrix(params string[] columns)
    {
        var matrix = ToImputedMatrix(columns);
        int nRows = matrix.GetLength(0);
        int nCols = matrix.GetLength(1);

        if (nRows < 2) return matrix;

        for (int j = 0; j < nCols; j++)
        {
            // 평균 계산
            double sum = 0;
            for (int i = 0; i < nRows; i++) sum += matrix[i, j];
            double mean = sum / nRows;

            // 표준편차 계산
            double ssq = 0;
            for (int i = 0; i < nRows; i++) ssq += (matrix[i, j] - mean) * (matrix[i, j] - mean);
            double std = Math.Sqrt(ssq / (nRows - 1));

            // 상수 컬럼이면 0으로 설정
            if (std < 1e-10)
            {
                for (int i = 0; i < nRows; i++) matrix[i, j] = 0;
            }
            else
            {
                for (int i = 0; i < nRows; i++) matrix[i, j] = (matrix[i, j] - mean) / std;
            }
        }

        return matrix;
    }
}
