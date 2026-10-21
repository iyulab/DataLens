# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Maintained from 0.14.1 onward; earlier releases are recorded by their tags only.

## [Unreleased]

### Changed

- Depends on `UInsight` 0.36.2 (by way of 0.36.1).
- Depends on `FilePrepper` 0.11.1, whose dependency graph already carries
  `System.Security.Cryptography.Pkcs` and `.Xml` 10.0.13. The direct
  references DataLens held to lift those two packages past their advisories
  are removed; the resolved versions are unchanged.

## [0.19.1] - 2026-10-18

### Changed

- Depends on `UInsight` 0.36.0 (by way of 0.35.0). Neither changes what a
  report says: their additions (`lof` by contamination, regression fitted
  values and residuals, the reason a normality test is absent) are in
  u-insight's WebAssembly and Rust API, the C functions DataLens calls keep
  their behaviour, and DataLens computes its univariate Hampel fences itself.

## [0.19.0] - 2026-10-16

### Changed

- Depends on `UInsight` 0.34.0 (by way of 0.33.0, which added `GetCodes()`,
  not called here). Two changes reach a report:
  - **Mahalanobis outliers** are judged against the distance's exact law
    (a scaled Beta, not χ²): on small samples more rows are flagged — the
    χ² threshold flagged far fewer than its level said, and with few rows
    none at all. Mahalanobis now needs at least `columns + 2` rows; below
    that the analyzer reports a warning instead of a result.
  - **Profiling**: a column's `StdDev` is `null` when the data does not
    define it (a single value), not `NaN`; `Mean` / `StdDev` / `Min` / `Max`
    stay `null` for a non-numeric column.

## [0.18.8] - 2026-10-15

### Changed

- Depends on `UInsight` 0.32.1 and `FilePrepper` 0.11.0 (was 0.7.4).

### Security

- `System.Security.Cryptography.Pkcs` and `System.Security.Cryptography.Xml`, which
  `FilePrepper` brings in through `EPPlus` at 10.0.10, are lifted to 10.0.13 — past
  the denial-of-service advisories GHSA-846f-f764-76mx, GHSA-2fmv-rv2c-59x7 (Pkcs)
  and GHSA-7hqp-7c5m-4vq6 (Xml), all fixed in 10.0.13.

## [0.18.7] - 2026-10-14

### Changed

- Depends on `UInsight` 0.32.0.

## [0.18.6] - 2026-10-13

### Changed

- Depends on `UInsight` 0.31.0.

## [0.18.5] - 2026-10-13

### Changed

- Follows `UInsight` 0.30.0. An analysis the engine refuses now carries a precise
  reason in the warning: too few rows is "insufficient data" (with the rows needed)
  rather than "degenerate data", an option outside its range names its bounds, and
  empty input is reported as such.

## [0.18.4] - 2026-10-08

### Fixed

- Follows `UInsight` 0.29.1, which loads the right native library on Linux arm64 and
  Intel macOS when the application is built without a runtime identifier.

## [0.18.3] - 2026-10-07

### Changed

- Follows `UInsight` 0.29.0: a row whose length differs and a non-finite cell are
  refused with their position. DataLens imputes missing
  values before the multivariate analyses, so its output is unchanged.
- Runs on `linux-arm64`: `UInsight` 0.29.0 carries a native library for it.

## [0.18.2] - 2026-10-07

### Changed

- Depends on `UInsight` 0.28.0 (from 0.26.0). DataLens's own analyses and
  reports are unchanged: the UInsight surfaces that moved (statistical process
  control, moved to UAnalytics in 0.27.0; distribution fitting's candidates in
  0.28.0) are ones DataLens does not call.

## [0.18.1] - 2026-10-03

### Changed

- Depends on `UInsight` 0.26.0. Its `InsightException` adds `Reason` and
  `Details` (the refusal's code and fields). Some analyses now refuse input
  they used to adjust: `lof` a `k` outside 1..=n-1, `gap_statistic` a `k_max`
  above the number of points, `permutation_importance` a NaN target. The
  cluster analyzer bounds its gap-statistic search by the row count itself,
  so small datasets cluster as before.

## [0.18.0] - 2026-10-01

### Added

- `SeriesAnomalyPoint.NearEdge` -- whether a point lies within a few places of
  either end of its batch, where the transform's boundary handling moves the
  saliency most. A position, not a verdict: a real anomaly there is still
  `IsAnomaly`.

### Changed

- Depends on `UInsight` 0.24.0. Its C# surface and the native analyses it calls
  are unchanged; the release changed the library's WebAssembly binding.

## [0.17.1] - 2026-09-30

Depends on `UInsight` 0.23.1 (0.23.0 before), whose HDBSCAN refuses
`min_samples` above the number of points and returns all noise when
`min_cluster_size` exceeds it -- both used to come back as one cluster holding
every point.

### Fixed

- **A clustering, feature, distribution, regression or changepoint step that
  fails is reported in `warnings` instead of being dropped silently.** The
  report field stays empty as before (or, for the gap statistic, K falls back
  to 3), and an `AnalysisWarning` names the step, the affected columns where
  there are any, and the engine's error category.

## [0.17.0] - 2026-09-29

### Changed

- **`AnalysisOptions.ChangepointCost` is a `ChangepointCost` enum**
  (`L2` or `Normal`) instead of a `uint` code. Code that assigned a number no
  longer compiles; write `ChangepointCost.Normal` for what was `1`.
- Built on UInsight 0.23.0 (0.22.0 before). Two of its changes reach DataLens
  reports:
  - K-means and HDBSCAN clusters are numbered by first appearance (the first
    row is in cluster 0), as DBSCAN and hierarchical clusters already were.
    The grouping is unchanged; the numbers attached to it can differ.
  - Permutation importance no longer depends on column order or names, so its
    scores for the same seed differ from earlier versions.

### Fixed

- **The hierarchical clustering report used single linkage, not Ward.** The
  analyzer passed the linkage code `0` — single linkage — under a comment
  saying Ward. Single linkage chains clusters together through intermediate
  points, so `Hierarchical` in the cluster report could differ markedly from
  the Ward result it claimed to be. It now requests `Linkage.Ward`, which
  UInsight 0.23.0 makes a named value instead of a number.

## [0.16.0] - 2026-09-25

### Changed

- `TimeSeriesAnalyzer.SpectralResidual` no longer keeps its own copy of the
  engine's option rules. An option out of range is still refused with an
  `ArgumentException` whose message starts with the option's name, but the
  rule in it is now the engine's, and the engine's `InsightException` is its
  `InnerException`. This layer checks only that a count is not negative,
  which the engine's unsigned type cannot hold.
- UInsight 0.21.0 -> 0.22.0: the Mahalanobis threshold is the exact
  chi-squared quantile (with one or two variables it was 1.9 % low, so
  `OutlierAnalyzer` flags slightly fewer points there), and `BatchSize = 0`
  is refused rather than running as a single batch.

## [0.15.0] - 2026-09-20

### Added

- `TimeSeriesAnalyzer` — two primitives over an ordered series: `EstimatePeriod`
  (the dominant period, or none) and `SpectralResidual` (a per-point anomaly score
  with an expected value and band, and the flagged indices). Both run on UInsight's
  own transform, so they work on every platform UInsight ships for, and neither
  takes a trained model. The results are DataLens types (`SeriesPeriod`,
  `SeriesAnomalyReport`); UInsight types do not appear in the signatures. An option
  out of range is refused with an `ArgumentException` naming that option.

### Changed

- The publish workflow checks for this version's changelog heading before it
  packs anything, instead of leaving that to CI, which runs beside it.

## [0.14.3] - 2026-09-17

### Fixed

- The `DataLens.CLI` package now carries the README, so its package page has a
  description like the library package does.

### Changed

- UInsight 0.20.1 (the code of 0.20.0, which never became installable on nuget.org).
  **This one reaches results DataLens shows.** The Jarque-Bera
  statistic was computed from the bias-adjusted skewness and kurtosis, where the
  test it cites uses the plain moment ratios, and its p-value lost the tail
  beyond a statistic of about 74. `ColumnDistribution.JbStatistic` and
  `JbPValue` are passed straight through, so both change; and because a column
  is called normal only when no test rejects, a Jarque-Bera p-value that moves
  across the significance level flips `IsNormal`, and with it `Shape`. Expect
  distribution sections to differ from 0.14.2 on data near the threshold.
- UInsight 0.19.0. The Mahalanobis outlier threshold now comes from an exact normal
  quantile (it was accurate to 4.5e-4), so flagged distances can differ in their
  trailing digits; the Box-Cox capability call it adds options to is not one
  DataLens reaches.
- UInsight 0.18.0. Nothing DataLens calls changed: the version adds period
  estimation and spectral residual anomaly scoring, which DataLens does not
  reach.
- UInsight 0.17.0. Nothing DataLens calls changed: the version reports only
  long-term capability indices when no within sigma is supplied and adds the
  charts' `SigmaHat`, neither of which DataLens reaches.
- UInsight 0.16.0. None of the charts DataLens calls changed; the new version
  takes subgroup sizes up to 25 and rejects a chart row it cannot use instead of
  skipping it.

## [0.14.2] - 2026-09-10

### Changed

- Track `UInsight` 0.15.0, which follows `u-analytics` 0.8. Nothing in this
  library's own surface changes; the pin moves so the dependency does not sit on
  an older snapshot while looking current.
- Re-synchronised the `UInsight` dependency with its latest published release.

### Added

- Security policy, contribution guide, and issue templates.
- NuGet security audit gate in CI: package advisories are warnings locally and
  errors in the audit pipeline.

## [0.14.1] - 2026-08-29

- Baseline for this changelog.
