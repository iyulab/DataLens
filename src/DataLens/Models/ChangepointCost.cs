namespace DataLens;

/// <summary>
/// PELT 변화점 탐지 비용 함수. UInsight <c>PeltCost</c> 의 DataLens-side 미러.
/// </summary>
public enum ChangepointCost
{
    /// <summary>평균 변화 탐지 (분산 고정 가정).</summary>
    L2,

    /// <summary>평균과 분산의 변화를 함께 탐지.</summary>
    Normal,
}
