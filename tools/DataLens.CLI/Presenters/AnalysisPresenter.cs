using DataLens.Models;
using Spectre.Console;

namespace DataLens.CLI.Presenters;

internal static class AnalysisPresenter
{
    public static void RenderAll(AnalysisResult result)
    {
        if (result.Profile is not null)
            RenderProfile(result.Profile);

        if (result.Descriptive is not null)
            RenderDescriptive(result.Descriptive);

        if (result.Correlation is not null)
            RenderCorrelation(result.Correlation);

        if (result.Regression is not null)
            RenderRegression(result.Regression);

        if (result.Distribution is not null)
            RenderDistribution(result.Distribution);

        if (result.Clusters is not null)
            RenderClusters(result.Clusters);

        if (result.Outliers is not null)
            RenderOutliers(result.Outliers);

        if (result.Features is not null)
            RenderFeatures(result.Features);

        if (result.Pca is not null)
            RenderPca(result.Pca);
    }

    public static void RenderProfile(ProfileReport report)
    {
        AnsiConsole.MarkupLine($"[bold underline]Profile[/]  ({report.RowCount:N0} rows x {report.ColumnCount} columns)");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("#")
            .AddColumn("Column")
            .AddColumn("Type")
            .AddColumn(new TableColumn("Valid").RightAligned())
            .AddColumn(new TableColumn("Null").RightAligned())
            .AddColumn(new TableColumn("Null%").RightAligned())
            .AddColumn(new TableColumn("Mean").RightAligned())
            .AddColumn(new TableColumn("Std").RightAligned())
            .AddColumn(new TableColumn("Min").RightAligned())
            .AddColumn(new TableColumn("Max").RightAligned());

        foreach (var col in report.Columns)
        {
            table.AddRow(
                col.Index.ToString(),
                Markup.Escape(col.Name),
                col.DataType,
                col.ValidCount.ToString("N0"),
                col.NullCount.ToString("N0"),
                col.NullPercentage.ToString("F1"),
                Fmt(col.Mean),
                Fmt(col.StdDev),
                Fmt(col.Min),
                Fmt(col.Max));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    public static void RenderDescriptive(DescriptiveReport report)
    {
        AnsiConsole.MarkupLine("[bold underline]Descriptive Statistics[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Column")
            .AddColumn(new TableColumn("Count").RightAligned())
            .AddColumn(new TableColumn("Mean").RightAligned())
            .AddColumn(new TableColumn("Median").RightAligned())
            .AddColumn(new TableColumn("Std").RightAligned())
            .AddColumn(new TableColumn("Min").RightAligned())
            .AddColumn(new TableColumn("Q1").RightAligned())
            .AddColumn(new TableColumn("Q3").RightAligned())
            .AddColumn(new TableColumn("Max").RightAligned())
            .AddColumn(new TableColumn("Skew").RightAligned())
            .AddColumn(new TableColumn("Kurt").RightAligned());

        foreach (var col in report.Columns)
        {
            table.AddRow(
                Markup.Escape(col.Name),
                col.Count.ToString("N0"),
                Fmt(col.Mean),
                Fmt(col.Median),
                Fmt(col.Std),
                Fmt(col.Min),
                Fmt(col.Q1),
                Fmt(col.Q3),
                Fmt(col.Max),
                Fmt(col.Skewness),
                Fmt(col.Kurtosis));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    public static void RenderCorrelation(CorrelationReport report)
    {
        AnsiConsole.MarkupLine("[bold underline]Correlation Analysis[/]");
        AnsiConsole.WriteLine();

        // Full matrix for small column counts
        if (report.Matrix is not null && report.ColumnNames.Count <= 8)
        {
            var table = new Table().Border(TableBorder.Rounded).AddColumn("");
            foreach (var name in report.ColumnNames)
                table.AddColumn(new TableColumn(Markup.Escape(Truncate(name, 10))).RightAligned());

            int n = report.ColumnNames.Count;
            for (int i = 0; i < n; i++)
            {
                var cells = new List<string> { Markup.Escape(Truncate(report.ColumnNames[i], 10)) };
                for (int j = 0; j < n; j++)
                {
                    var v = report.Matrix[i, j];
                    var text = double.IsNaN(v) ? "-" : v.ToString("F2");
                    if (i != j && Math.Abs(v) >= 0.7)
                        text = $"[bold yellow]{text}[/]";
                    cells.Add(text);
                }
                table.AddRow(cells.ToArray());
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        // High correlation pairs
        if (report.HighCorrelationPairs.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]High Correlation Pairs[/] (|r| >= threshold)");
            var pairTable = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Column 1")
                .AddColumn("Column 2")
                .AddColumn(new TableColumn("r").RightAligned())
                .AddColumn(new TableColumn("|r|").RightAligned());

            foreach (var pair in report.HighCorrelationPairs.OrderByDescending(p => p.AbsValue))
            {
                pairTable.AddRow(
                    Markup.Escape(pair.Column1),
                    Markup.Escape(pair.Column2),
                    pair.Value.ToString("F4"),
                    pair.AbsValue.ToString("F4"));
            }

            AnsiConsole.Write(pairTable);
            AnsiConsole.WriteLine();
        }

        // Categorical associations
        if (report.CategoricalAssociations.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Categorical Associations[/] (Cramer's V)");
            var catTable = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Column 1")
                .AddColumn("Column 2")
                .AddColumn(new TableColumn("Cramer's V").RightAligned())
                .AddColumn(new TableColumn("p-value").RightAligned());

            foreach (var assoc in report.CategoricalAssociations.OrderByDescending(a => a.CramersV))
            {
                catTable.AddRow(
                    Markup.Escape(assoc.Column1),
                    Markup.Escape(assoc.Column2),
                    assoc.CramersV.ToString("F4"),
                    assoc.PValue.ToString("E2"));
            }

            AnsiConsole.Write(catTable);
            AnsiConsole.WriteLine();
        }
    }

    public static void RenderRegression(RegressionReport report)
    {
        AnsiConsole.MarkupLine($"[bold underline]Regression Analysis[/]  (target: {Markup.Escape(report.TargetColumn ?? "N/A")})");
        AnsiConsole.WriteLine();

        if (report.Entries.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim]No regression entries available.[/]");
            AnsiConsole.WriteLine();
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Feature")
            .AddColumn(new TableColumn("Slope").RightAligned())
            .AddColumn(new TableColumn("Intercept").RightAligned())
            .AddColumn(new TableColumn("R^2").RightAligned())
            .AddColumn(new TableColumn("Adj R^2").RightAligned())
            .AddColumn(new TableColumn("F p-value").RightAligned());

        foreach (var entry in report.Entries.OrderByDescending(e => e.RSquared))
        {
            var r2Text = entry.RSquared.ToString("F4");
            if (entry.RSquared >= 0.7) r2Text = $"[bold green]{r2Text}[/]";
            else if (entry.RSquared >= 0.3) r2Text = $"[yellow]{r2Text}[/]";

            table.AddRow(
                Markup.Escape(entry.FeatureColumn),
                entry.Slope.ToString("F4"),
                entry.Intercept.ToString("F4"),
                r2Text,
                entry.AdjRSquared.ToString("F4"),
                entry.FPValue.ToString("E2"));
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    public static void RenderDistribution(DistributionReport report)
    {
        AnsiConsole.MarkupLine("[bold underline]Distribution / Normality Tests[/]");
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Column")
            .AddColumn(new TableColumn("N").RightAligned())
            .AddColumn(new TableColumn("SW stat").RightAligned())
            .AddColumn(new TableColumn("SW p").RightAligned())
            .AddColumn(new TableColumn("JB stat").RightAligned())
            .AddColumn(new TableColumn("JB p").RightAligned())
            .AddColumn("Normal?");

        foreach (var col in report.Columns)
        {
            var normalText = col.IsNormal
                ? "[green]Yes[/]"
                : "[red]No[/]";

            table.AddRow(
                Markup.Escape(col.Name),
                col.SampleSize.ToString("N0"),
                col.SwStatistic.ToString("F4"),
                col.SwPValue.ToString("E2"),
                col.JbStatistic.ToString("F2"),
                col.JbPValue.ToString("E2"),
                normalText);
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    public static void RenderClusters(ClusterReport report)
    {
        AnsiConsole.MarkupLine("[bold underline]Clustering[/]");
        AnsiConsole.WriteLine();

        if (report.OptimalK.HasValue)
            AnsiConsole.MarkupLine($"  Optimal K (Gap Statistic): [bold]{report.OptimalK}[/]");

        if (report.KMeans is not null)
        {
            AnsiConsole.MarkupLine($"  [bold]K-Means[/] (K={report.KMeans.K}, WCSS={report.KMeans.Wcss:F2}, iterations={report.KMeans.Iterations})");
            if (report.KMeans.ClusterSizes.Count > 0)
            {
                var table = new Table()
                    .Border(TableBorder.Simple)
                    .AddColumn("Cluster")
                    .AddColumn(new TableColumn("Size").RightAligned())
                    .AddColumn(new TableColumn("%").RightAligned());

                foreach (var cs in report.KMeans.ClusterSizes)
                    table.AddRow(cs.ClusterId.ToString(), cs.Size.ToString("N0"), cs.Percentage.ToString("F1"));

                AnsiConsole.Write(table);
            }
        }

        if (report.Dbscan is not null)
            AnsiConsole.MarkupLine($"  [bold]DBSCAN[/]: {report.Dbscan.NClusters} clusters, {report.Dbscan.NoiseCount} noise points");

        if (report.Hierarchical is not null)
            AnsiConsole.MarkupLine($"  [bold]Hierarchical[/]: {report.Hierarchical.NClusters} clusters");

        AnsiConsole.WriteLine();
    }

    public static void RenderOutliers(OutlierReport report)
    {
        AnsiConsole.MarkupLine("[bold underline]Outlier Detection[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  Total rows: {report.TotalRows:N0}");
        AnsiConsole.MarkupLine($"  Outliers: [bold]{report.OutlierCount:N0}[/] ({report.OutlierPercentage:F1}%)");

        if (report.IsolationForest is not null)
            AnsiConsole.MarkupLine($"  IsolationForest: {report.IsolationForest.AnomalyCount} anomalies (threshold={report.IsolationForest.Threshold:F4})");
        if (report.Lof is not null)
            AnsiConsole.MarkupLine($"  LOF: {report.Lof.AnomalyCount} anomalies (threshold={report.Lof.Threshold:F4})");
        if (report.Mahalanobis is not null)
            AnsiConsole.MarkupLine($"  Mahalanobis: {report.Mahalanobis.OutlierCount} outliers (threshold={report.Mahalanobis.Threshold:F4})");

        AnsiConsole.WriteLine();
    }

    public static void RenderFeatures(FeatureReport report)
    {
        AnsiConsole.MarkupLine($"[bold underline]Feature Importance[/]  (target: {Markup.Escape(report.TargetColumn ?? "N/A")})");
        AnsiConsole.WriteLine();

        if (report.Importance is not null && report.Importance.Scores.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Importance Scores[/]");
            var chart = new BarChart().Width(60);
            foreach (var score in report.Importance.Scores.OrderByDescending(s => s.Score).Take(20))
                chart.AddItem(Truncate(score.Name, 20), Math.Max(0, score.Score * 100), Color.Blue);
            AnsiConsole.Write(chart);
            AnsiConsole.WriteLine();
        }

        if (report.Anova is not null && report.Anova.Features.Count > 0)
        {
            AnsiConsole.MarkupLine($"[bold]ANOVA[/] ({report.Anova.SelectedCount} selected features)");
            var table = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Feature")
                .AddColumn(new TableColumn("F-stat").RightAligned())
                .AddColumn(new TableColumn("p-value").RightAligned());

            foreach (var f in report.Anova.Features.OrderByDescending(f => f.FStatistic).Take(15))
                table.AddRow(Markup.Escape(f.Name), f.FStatistic.ToString("F2"), f.PValue.ToString("E2"));

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        if (report.MutualInfo is not null && report.MutualInfo.Features.Count > 0)
        {
            AnsiConsole.MarkupLine("[bold]Mutual Information[/]");
            var table = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Feature")
                .AddColumn(new TableColumn("MI").RightAligned());

            foreach (var f in report.MutualInfo.Features.OrderByDescending(f => f.Mi).Take(15))
                table.AddRow(Markup.Escape(f.Name), f.Mi.ToString("F4"));

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }

        if (report.Permutation is not null && report.Permutation.Features.Count > 0)
        {
            AnsiConsole.MarkupLine($"[bold]Permutation Importance[/] (baseline={report.Permutation.BaselineScore:F4})");
            var table = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("Feature")
                .AddColumn(new TableColumn("Importance").RightAligned())
                .AddColumn(new TableColumn("StdDev").RightAligned());

            foreach (var f in report.Permutation.Features.OrderByDescending(f => f.Importance).Take(15))
                table.AddRow(Markup.Escape(f.Name), f.Importance.ToString("F4"), f.StdDev.ToString("F4"));

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();
        }
    }

    public static void RenderPca(PcaReport report)
    {
        AnsiConsole.MarkupLine("[bold underline]PCA[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"  Components: {report.NComponents}");
        AnsiConsole.MarkupLine($"  Total explained variance: {report.TotalExplainedVariance:F4}");

        if (report.ExplainedVariance.Length > 0)
        {
            var table = new Table()
                .Border(TableBorder.Simple)
                .AddColumn("PC")
                .AddColumn(new TableColumn("Variance").RightAligned())
                .AddColumn(new TableColumn("Cumulative").RightAligned());

            for (int i = 0; i < report.ExplainedVariance.Length; i++)
            {
                table.AddRow(
                    $"PC{i + 1}",
                    report.ExplainedVariance[i].ToString("F4"),
                    i < report.CumulativeVariance.Length ? report.CumulativeVariance[i].ToString("F4") : "-");
            }

            AnsiConsole.Write(table);
        }

        AnsiConsole.WriteLine();
    }

    private static string Fmt(double? value) => value.HasValue ? Fmt(value.Value) : "-";
    private static string Fmt(double value) => double.IsNaN(value) ? "-" : value.ToString("G6");

    private static string Truncate(string text, int maxLength)
        => text.Length <= maxLength ? text : text[..(maxLength - 2)] + "..";
}
