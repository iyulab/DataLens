#!/usr/bin/env bash
#
# The <Version> in Directory.Build.props must have a matching heading in
# CHANGELOG.md.
#
# A release whose version has no changelog heading ships as "[Unreleased]" to
# everyone who reads the package page. The registry check in publish.yml only
# prevents re-publishing the same version; it says nothing about whether the new
# one was written down.
#
# Run locally before pushing a version bump:
#   bash scripts/check-changelog-entry.sh
#
# Enforced in CI (.github/workflows/ci.yml, "Changelog entry" job) and before
# anything is packed in .github/workflows/publish.yml, which does not wait for CI.
#
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

props="Directory.Build.props"
version="$(grep -m1 -oE '<Version>[^<]+</Version>' "${props}" | sed -E 's#</?Version>##g')"
if [[ -z "${version}" ]]; then
  echo "::error file=${props}::could not read <Version>"
  exit 1
fi
echo "Directory.Build.props declares ${version}"

# Escape the dots so `0.1.0` cannot match a heading like `0X1Y0`.
if grep -qE "^## \[${version//./\.}\]" CHANGELOG.md; then
  echo "CHANGELOG.md has an entry for ${version}"
  exit 0
fi

echo "::error file=CHANGELOG.md::no '## [${version}]' heading — add the entry for this release in the same commit as the version bump (move it out of '## [Unreleased]')"
exit 1
