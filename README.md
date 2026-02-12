# DataLens

A .NET library for exploratory data analysis, statistical profiling, and interactive report generation.

## Overview

DataLens answers the question: **"What's in my data?"** — before you clean it, before you model it.

Given any CSV dataset, DataLens produces comprehensive statistical analysis and visual reports that help you understand distributions, relationships, patterns, and anomalies. It combines [FilePrepper](https://github.com/iyulab/FilePrepper) for data ingestion with [u-insight](https://github.com/iyulab/u-insight) (Rust FFI) for high-performance computation.

## Where DataLens Fits

```
CSV Data
  │
  ├── "Understand" → DataLens    → Reports, charts, insights
  │
  ├── "Clean"      → FilePrepper → Cleaned CSV
  │
  └── "Predict"    → MLoop       → Models, predictions
```

| Tool | Purpose | Input | Output |
|------|---------|-------|--------|
| **DataLens** | Understand your data | CSV | HTML reports, analysis results |
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

// Generate a full analysis report
await DataLens.Analyze("manufacturing_data.csv")
    .ToHtml("report.html");
```

Open `report.html` in your browser — done.

### Programmatic Access

```csharp
using DataLens;

var analysis = await DataLens.Analyze("manufacturing_data.csv");

// Descriptive statistics
foreach (var col in analysis.Summary.Columns)
{
    Console.WriteLine($"{col.Name}: mean={col.Mean:F3}, skew={col.Skewness:F3}");
}

// High correlations
var correlated = analysis.Correlation
    .Where(r => Math.Abs(r.Value) > 0.7)
    .OrderByDescending(r => Math.Abs(r.Value));

// Detected clusters
var clusters = analysis.Clusters;
Console.WriteLine($"Optimal K={clusters.K}, Silhouette={clusters.Score:F3}");

// Outlier summary
Console.WriteLine($"Outliers: {analysis.Outliers.Count} rows ({analysis.Outliers.Percentage:F1}%)");
```

## Analysis Modules

### 1. Data Profiling

Comprehensive overview of your dataset at a glance.

- Row/column counts, memory usage, data types
- Missing value heatmap and completeness scores
- Duplicate detection
- Encoding detection and auto-conversion (CP949/EUC-KR → UTF-8)

```csharp
var profile = await DataLens.Profile("data.csv");
Console.WriteLine($"Rows: {profile.RowCount}, Columns: {profile.ColumnCount}");
Console.WriteLine($"Missing: {profile.MissingPercentage:F1}%");
Console.WriteLine($"Encoding: {profile.DetectedEncoding}"); // UTF-8, CP949, etc.
```

### 2. Descriptive Statistics

Full statistical summary for every numeric variable.

- Count, min, max, mean, median, mode
- Standard deviation, variance, standard error
- Skewness, kurtosis
- Percentiles (Q1, Q3, IQR)
- Zero/negative value counts

```csharp
var stats = analysis.Summary;
var col = stats["X_ActualVelocity"];
// mean=4.506, std=2.575, skew=0.065, kurtosis=-0.246
```

### 3. Correlation Analysis

Discover relationships between variables.

- Pearson, Spearman, and Kendall correlation matrices
- Automatic high-correlation pair detection
- Multicollinearity check (VIF)
- Interactive correlation heatmap in HTML reports

```csharp
var corr = analysis.Correlation;
var pairs = corr.HighCorrelationPairs(threshold: 0.8);
// [(X_ActualPosition, X_SetPosition, r=0.999), ...]
```

### 4. Regression Analysis

Quantify variable relationships.

- Simple and multiple linear regression
- R², adjusted R², ANOVA
- Residual diagnostics
- Coefficient significance tests

```csharp
var regression = await DataLens.Regress("data.csv",
    target: "S_OutputPower",
    features: new[] { "S_CurrentFeedback", "S_OutputVoltage", "S_ActualVelocity" });

Console.WriteLine($"R²={regression.RSquared:F4}");
```

### 5. Cluster Analysis

Find natural groupings in your data.

- K-Means with automatic K selection (elbow + silhouette)
- Hierarchical clustering with dendrogram visualization
- Cluster profiles with centroid descriptions

```csharp
var clusters = analysis.Clusters; // Auto K-Means
foreach (var cluster in clusters.Groups)
{
    Console.WriteLine($"Cluster {cluster.Id}: {cluster.Size} rows");
    Console.WriteLine($"  Centroid: {string.Join(", ", cluster.TopFeatures)}");
}
```

### 6. Outlier Detection

Identify anomalous data points.

- IQR-based detection
- Z-score method
- Per-column and multi-variate outlier flagging
- Severity scores

```csharp
var outliers = analysis.Outliers;
var flagged = outliers.GetRows(); // Row indices with outlier scores
```

### 7. Feature Importance

Understand which variables matter most.

- Correlation-based ranking against target variable
- Permutation importance
- Redundancy detection (groups of near-identical variables)

```csharp
var importance = await DataLens.FeatureImportance("data.csv", target: "Machining_Process");
foreach (var feat in importance.Top(10))
{
    Console.WriteLine($"  {feat.Name}: {feat.Score:F4}");
}
```

### 8. Dimensionality Reduction

Compress high-dimensional data for visualization.

- PCA with explained variance
- Component loading analysis
- 2D/3D scatter plot data for reports

```csharp
var pca = analysis.PCA;
Console.WriteLine($"Top 3 components explain {pca.ExplainedVariance(3):P1} of variance");
```

## Report Generation

DataLens generates self-contained HTML reports with interactive charts powered by Chart.js / Plotly.js.

```csharp
// Full report
await DataLens.Analyze("data.csv")
    .ToHtml("full-report.html");

// Selective report
await DataLens.Analyze("data.csv")
    .Include(Section.Summary, Section.Correlation, Section.Clusters)
    .ToHtml("focused-report.html");

// JSON output for integration
await DataLens.Analyze("data.csv")
    .ToJson("results.json");
```

### Report Sections

| Section | Contents |
|---------|----------|
| **Overview** | Dataset profile, shape, types, completeness |
| **Statistics** | Per-variable summary with distribution histograms |
| **Correlation** | Interactive heatmap with filterable thresholds |
| **Regression** | Coefficient tables, residual plots, R² metrics |
| **Clusters** | Scatter plots with cluster coloring, silhouette chart |
| **Outliers** | Flagged rows with severity, box plots |
| **Features** | Importance ranking, redundancy groups |
| **PCA** | Explained variance chart, 2D component scatter |

## Architecture

```
┌──────────────────────────────────────────┐
│           DataLens (C# .NET)             │
│                                          │
│  ┌──────────┐  ┌───────────────────────┐ │
│  │ Analysis  │  │ Report Generation     │ │
│  │ Pipeline  │  │  • HTML (Chart.js)    │ │
│  │           │  │  • JSON               │ │
│  └─────┬────┘  └───────────┬───────────┘ │
│        │                   │             │
├────────┴───────────────────┴─────────────┤
│  ┌──────────────┐  ┌──────────────────┐  │
│  │ FilePrepper   │  │ u-insight C#     │  │
│  │ (C# native)   │  │ (Rust FFI)      │  │
│  │               │  │                  │  │
│  │ • CSV parsing │  │ • Statistics     │  │
│  │ • Encoding    │  │ • Correlation    │  │
│  │ • Type detect │  │ • Clustering     │  │
│  │ • Missing val │  │ • PCA           │  │
│  └──────────────┘  │ • Outlier detect │  │
│                    │ • Regression     │  │
│                    └──────────────────┘  │
│                         │ FFI            │
│                    ┌────┴─────────────┐  │
│                    │ u-insight (Rust) │  │
│                    │ u-analytics      │  │
│                    │ u-numflow        │  │
│                    │ u-metaheur       │  │
│                    └─────────────────┘  │
└──────────────────────────────────────────┘
```

## Integration with iyulab Tools

### FilePrepper → DataLens

```csharp
// Clean first, then analyze
var cleaned = await FilePrepper.Process("raw_data.csv")
    .RemoveDuplicates()
    .FillMissing(Strategy.Median)
    .ToCsv("cleaned.csv");

var report = await DataLens.Analyze("cleaned.csv")
    .ToHtml("analysis.html");
```

### DataLens → MLoop

DataLens analysis results can guide MLoop training decisions:

```csharp
var analysis = await DataLens.Analyze("train.csv");

// Check which features matter before training
var topFeatures = analysis.FeatureImportance("target_column").Top(15);

// Check for multicollinearity issues
var vifWarnings = analysis.Correlation.VIF().Where(v => v.Score > 10);

// Then proceed to MLoop with confidence
// mloop train datasets/train.csv target_column --time 120
```

## Scope & Non-Goals

**In Scope:**
- Exploratory data analysis (EDA)
- Statistical profiling and summaries
- Relationship and pattern discovery
- Interactive HTML report generation
- JSON output for programmatic consumption
- FilePrepper integration for data ingestion
- Encoding detection (CP949/EUC-KR, UTF-8)

**Out of Scope:**
- Data cleaning / transformation (→ [FilePrepper](https://github.com/iyulab/FilePrepper))
- ML model training / prediction (→ [MLoop](https://github.com/iyulab/MLoop))
- Deep learning (CNN, LSTM, Autoencoder)
- Real-time streaming analysis
- Interactive notebook environments

## Requirements

- .NET 10.0+
- Dependencies: `FilePrepper`, `UInsight.Interop` (u-insight C# bindings)

## License

MIT License — Built by [iyulab](https://github.com/iyulab)