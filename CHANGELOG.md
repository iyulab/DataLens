# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Maintained from 0.14.1 onward; earlier releases are recorded by their tags only.

## [Unreleased]

## [0.15.0] - 2026-09-20

### Added

- `TimeSeriesAnalyzer` — two primitives over an ordered series: `EstimatePeriod`
  (the dominant period, or none) and `SpectralResidual` (a per-point anomaly score
  with an expected value and band, and the flagged indices). Both run on UInsight's
  own transform, so they work on every platform UInsight ships for, and neither
  takes a trained model. The results are DataLens types (`SeriesPeriod`,
  `SeriesAnomalyReport`); UInsight types do not appear in the signatures.

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
