namespace DataLens.Models;

public class CorrelationReport
{
    public List<string> ColumnNames { get; init; } = [];
    public double[,]? Matrix { get; init; }
    public List<CorrelationPair> HighCorrelationPairs { get; init; } = [];
    public List<CategoricalAssociation> CategoricalAssociations { get; init; } = [];
}

public class CorrelationPair
{
    public string Column1 { get; init; } = "";
    public string Column2 { get; init; } = "";
    public double Value { get; init; }
    public double AbsValue => Math.Abs(Value);
}

public class CategoricalAssociation
{
    public string Column1 { get; init; } = "";
    public string Column2 { get; init; } = "";
    public double CramersV { get; init; }
    public double ChiSquared { get; init; }
    public double PValue { get; init; }
}
