# Changelog

All notable changes to this project are documented here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

Maintained from 0.14.1 onward; earlier releases are recorded by their tags only.

## [Unreleased]

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
