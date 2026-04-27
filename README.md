# DataLens

[![NuGet](https://img.shields.io/nuget/v/DataLens.svg)](https://www.nuget.org/packages/DataLens)
[![NuGet Downloads](https://img.shields.io/nuget/dt/DataLens.svg)](https://www.nuget.org/packages/DataLens)
[![Build](https://github.com/iyulab/DataLens/actions/workflows/publish.yml/badge.svg)](https://github.com/iyulab/DataLens/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A .NET library for exploratory data analysis and statistical profiling.

## Overview

DataLens answers the question: **"What's in my data?"** — before you clean it, before you model it.

Given a CSV/JSON dataset, DataLens produces comprehensive statistical analysis that helps you understand distributions, relationships, patterns, and anomalies. It combines [FilePrepper](https://github.com/iyulab/FilePrepper) for data ingestion with [UInsight](https://github.com/iyulab/u-insight) (Rust FFI) for high-performance computation.

## Where DataLens Fits

```
CSV / JSON
  │
  ├── "Understand" → DataLens    → Analysis result + JSON
  │
  ├── "Clean"      → FilePrepper → Cleaned CSV
  │
  └── "Predict"    → MLoop       → Models, predictions
```

| Tool | Purpose | Input | Output |
|------|---------|-------|--------|
| **DataLens** | Understand your data | CSV / JSON | Analysis result objects, JSON |
| **FilePrepper** | Clean & transform data | CSV | Cleaned CSV |
| **MLoop** | Train & deploy ML models | CSV | ML model, predictions |

DataLens is not a replacement for FilePrepper or MLoop — it's the **first step** before either of them.

## Quick Start

### Installation

```bash
dotnet add package DataLens
```

### One-Line Analysis

```csharp
using DataLens;

// Run the full analysis pipeline and write the result as JSON
var analysis = await DataLensEngine.Analyze("manufacturing_data.csv");
await analysis.ToJsonAsync("results.json");
```

> HTML report generation (`ToHtml(...)`) is planned for a future release. See
> [issue: html-report-missing](claudedocs/issues/ISSUE-DataLens-20260427-html-report-missing.md).

### Programmatic Access

```csharp
using DataLens;

var analysis = await DataLensEngine.Analyze("manufacturing_data.csv");

// Profile (row/column counts, per-column null %, type, basic stats)
Console.WriteLine($"Rows: {analysis.Profile!.RowCount}, Cols: {analysis.Profile.ColumnCount}");
foreach (var col in analysis.Profile.Columns)
{
    Console.WriteLine($"{col.Name}: type={col.DataType}, null={col.NullPercentage:F1}%");
}

// Descriptive statistics (mean, std, skew, kurtosis, ...)
foreach (var col in analysis.Descriptive!.Columns)
{
    Console.WriteLine($"{col.Name}: mean={col.Mean:F3}, skew={col.Skewness:F3}");
}

// Correlation — high pairs already filtered by AnalysisOptions.CorrelationThreshold
foreach (var pair in analysis.Correlation!.HighCorrelationPairs)
{
    Console.WriteLine($"{pair.Column1} ~ {pair.Column2}: r={pair.Value:F3}");
}

// Clusters
var kmeans = analysis.Clusters!.KMeans;
if (kmeans is not null)
{
    Console.WriteLine($"K={kmeans.K}, WCSS={kmeans.Wcss:F3}");
    foreach (var cluster in kmeans.ClusterSizes)
    {
        Console.WriteLine($"  Cluster {cluster.ClusterId}: {cluster.Size} rows ({cluster.Percentage:F1}%)");
    }
}

// Outliers
Console.WriteLine($"Outliers: {analysis.Outliers!.OutlierCount} rows ({analysis.Outliers.OutlierPercentage:F1}%)");
```

### Selecting analyses

Use `AnalysisOptions` to enable/disable specific analyzers:

```csharp
var options = new AnalysisOptions
{
    IncludeProfiling   = true,
    IncludeDescriptive = true,
    IncludeCorrelation = true,
    IncludeClustering  = false,
    IncludeOutliers    = false,
    IncludeFeatures    = false,
    IncludePca         = false,
    IncludeChangepoints = false,
    CorrelationThreshold = 0.8
};

var analysis = await DataLensEngine.Analyze("data.csv", options);
var json = analysis.ToJson(Section.Correlation); // Single-section JSON
```

## Analysis Modules

### 1. Data Profiling

Per-column overview: type detection, null counts, basic numeric summary.

```csharp
var profile = await DataLensEngine.Profile("data.csv");
Console.WriteLine($"Rows: {profile.RowCount}, Columns: {profile.ColumnCount}");
foreach (var col in profile.Columns)
{
    Console.WriteLine($"{col.Name}: type={col.DataType}, null={col.NullPercentage:F1}%");
}
```

### 2. Descriptive Statistics

Full numeric summary per column: count, mean, median, std, variance, Q1/Q3/IQR, skewness, kurtosis.

```csharp
var analysis = await DataLensEngine.Analyze("data.csv");
foreach (var col in analysis.Descriptive!.Columns)
{
    Console.WriteLine($"{col.Name}: mean={col.Mean:F3}, std={col.Std:F3}, skew={col.Skewness:F3}");
}
```

### 3. Correlation Analysis

- Pearson correlation matrix over numeric columns
- High-correlation pairs auto-filtered by `AnalysisOptions.CorrelationThreshold`
- Cramér's V for categorical associations

```csharp
var corr = analysis.Correlation!;
foreach (var pair in corr.HighCorrelationPairs)
{
    Console.WriteLine($"{pair.Column1} ~ {pair.Column2}: r={pair.Value:F3}");
}
```

### 4. Regression Analysis

OLS-based regression against `AnalysisOptions.TargetColumn`.

```csharp
var options = new AnalysisOptions { TargetColumn = "S_OutputPower", IncludeRegression = true };
var analysis = await DataLensEngine.Analyze("data.csv", options);
var regression = analysis.Regression!;
Console.WriteLine($"R²={regression.RSquared:F4}");
```

### 5. Cluster Analysis

K-Means (with auto-K via Gap statistic), DBSCAN, Hierarchical, HDBSCAN.

```csharp
var clusters = analysis.Clusters!;
Console.WriteLine($"Optimal K={clusters.OptimalK}");
if (clusters.KMeans is { } km)
{
    foreach (var cluster in km.ClusterSizes)
    {
        Console.WriteLine($"Cluster {cluster.ClusterId}: {cluster.Size} rows");
    }
}
```

### 6. Outlier Detection

Isolation Forest, LOF, and Mahalanobis distance.

```csharp
var outliers = analysis.Outliers!;
Console.WriteLine($"Outliers: {outliers.OutlierCount} rows ({outliers.OutlierPercentage:F1}%)");
if (outliers.IsolationForest is { } iso)
{
    Console.WriteLine($"  IsolationForest: {iso.AnomalyCount} anomalies (threshold={iso.Threshold:F3})");
}
```

### 7. Feature Importance

ANOVA F-test, mutual information, and permutation importance against a target column.

```csharp
var report = await DataLensEngine.FeatureImportance("data.csv", target: "Machining_Process");
foreach (var feat in report.Importance!.Scores)
{
    Console.WriteLine($"  {feat.Name}: {feat.Score:F4}");
}
```

### 8. Dimensionality Reduction (PCA)

```csharp
var pca = analysis.Pca!;
Console.WriteLine($"Components: {pca.NComponents}, total variance explained: {pca.TotalExplainedVariance:P1}");
for (int i = 0; i < pca.ExplainedVariance.Length; i++)
{
    Console.WriteLine($"  PC{i + 1}: {pca.ExplainedVariance[i]:P1}");
}
```

### 9. Changepoint Detection

PELT-based changepoint detection (multivariate, configurable cost function).

```csharp
var options = new AnalysisOptions
{
    IncludeChangepoints = true,
    ChangepointCost = 1, // 0=L2 mean, 1=Normal mean+variance
    ChangepointMinSegmentLength = 10
};
var analysis = await DataLensEngine.Analyze("timeseries.csv", options);
```

## Output

DataLens currently emits results as JSON. HTML report generation is planned.

```csharp
// Full result
var json = analysis.ToJson();
await analysis.ToJsonAsync("results.json");

// Section-scoped JSON
var corrJson = analysis.ToJson(Section.Correlation);
```

`Section` members: `Profile`, `Descriptive`, `Correlation`, `Regression`,
`Clusters`, `Outliers`, `Distribution`, `Features`, `Pca`, `Changepoints`.

## Architecture

```
┌──────────────────────────────────────────┐
│           DataLens (C# .NET)             │
│                                          │
│  ┌──────────┐  ┌───────────────────────┐ │
│  │ Analysis  │  │ JSON Serializer       │ │
│  │ Pipeline  │  │  (HTML reports        │ │
│  │           │  │   planned)            │ │
│  └─────┬────┘  └───────────┬───────────┘ │
│        │                   │             │
├────────┴───────────────────┴─────────────┤
│  ┌──────────────┐  ┌──────────────────┐  │
│  │ FilePrepper   │  │ UInsight (C#)    │  │
│  │ (C# native)   │  │ ↓ FFI            │  │
│  │               │  │ UInsight (Rust)  │  │
│  │ • CSV / JSON  │  │                  │  │
│  │ • DataFrame   │  │ • Statistics     │  │
│  │ • Type detect │  │ • Correlation    │  │
│  │               │  │ • Clustering     │  │
│  └──────────────┘  │ • PCA            │  │
│                    │ • Outlier detect │  │
│                    │ • Regression     │  │
│                    │ • Changepoints   │  │
│                    └──────────────────┘  │
└──────────────────────────────────────────┘
```

## Integration with iyulab Tools

### FilePrepper → DataLens

DataLens uses FilePrepper internally for CSV/JSON ingestion via `CsvBridge`.
For pre-cleaning, run a FilePrepper pipeline and feed the resulting CSV to
DataLens (or pass a `DataFrame` directly):

```csharp
using FilePrepper.Pipeline;

var pipeline = await DataPipeline.FromCsvAsync("raw_data.csv");
// ... apply FilePrepper transforms ...
var df = pipeline.ToDataFrame();

var analysis = await DataLensEngine.Analyze(df);
```

### DataLens → MLoop

DataLens analysis results can guide MLoop training decisions:

```csharp
var options = new AnalysisOptions { TargetColumn = "target_column", IncludeFeatures = true };
var analysis = await DataLensEngine.Analyze("train.csv", options);

// Top features by ANOVA F-score
var topByAnova = analysis.Features!.Anova!.Features
    .OrderByDescending(f => f.FStatistic)
    .Take(15);

// High-correlation pairs (multicollinearity hints)
foreach (var pair in analysis.Correlation!.HighCorrelationPairs)
{
    Console.WriteLine($"{pair.Column1} ~ {pair.Column2}: r={pair.Value:F3}");
}

// Then proceed to MLoop with confidence:
// mloop train datasets/train.csv target_column --time 120
```

## Scope & Non-Goals

**In Scope:**
- Exploratory data analysis (EDA)
- Statistical profiling and summaries
- Relationship and pattern discovery (correlation, clustering, PCA)
- Outlier and changepoint detection
- JSON output for programmatic consumption
- CSV / JSON ingestion via FilePrepper

**Out of Scope:**
- Data cleaning / transformation (→ [FilePrepper](https://github.com/iyulab/FilePrepper))
- ML model training / prediction (→ [MLoop](https://github.com/iyulab/MLoop))
- Deep learning (CNN, LSTM, Autoencoder)
- Real-time streaming analysis
- Interactive notebook environments

**Planned (not yet shipped):**
- HTML reports with interactive charts (Plotly.js)
- Encoding auto-detection in `CsvBridge` — depends on FilePrepper 0.7.0
- `IEnumerable<T>` POCO input
- Fluent facade API + `examples/` build verification

## Requirements

- .NET 10.0+
- Dependencies: [`FilePrepper`](https://www.nuget.org/packages/FilePrepper), [`UInsight`](https://www.nuget.org/packages/UInsight)

## License

MIT License — Built by [iyulab](https://github.com/iyulab)
