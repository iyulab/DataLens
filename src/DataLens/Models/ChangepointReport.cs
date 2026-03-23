namespace DataLens.Models;

/// <summary>
/// 변화점(Changepoint) 탐지 결과.
/// </summary>
public class ChangepointReport
{
    /// <summary>컬럼별 변화점 분석 결과.</summary>
    public List<ColumnChangepointResult> Columns { get; init; } = [];
}

/// <summary>
/// 단일 컬럼의 PELT 변화점 탐지 결과.
/// </summary>
public class ColumnChangepointResult
{
    /// <summary>컬럼명.</summary>
    public string Name { get; init; } = "";

    /// <summary>데이터 포인트 수.</summary>
    public int SampleSize { get; init; }

    /// <summary>탐지된 변화점 인덱스 (0-based).</summary>
    public uint[] Changepoints { get; init; } = [];

    /// <summary>세그먼트 수 (변화점 + 1).</summary>
    public uint NSegments { get; init; }

    /// <summary>세그먼트별 요약 통계.</summary>
    public List<SegmentSummary> Segments { get; init; } = [];
}

/// <summary>
/// 변화점으로 분할된 세그먼트의 요약 통계.
/// </summary>
public class SegmentSummary
{
    /// <summary>세그먼트 시작 인덱스 (inclusive).</summary>
    public int Start { get; init; }

    /// <summary>세그먼트 끝 인덱스 (exclusive).</summary>
    public int End { get; init; }

    /// <summary>세그먼트 길이.</summary>
    public int Length { get; init; }

    /// <summary>세그먼트 평균.</summary>
    public double Mean { get; init; }

    /// <summary>세그먼트 표준편차.</summary>
    public double StdDev { get; init; }
}
