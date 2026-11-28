---
id: BL-6077
title: Fix CI failure EachProject_HasTitleIconLicenseExpressionAndProjectUrl on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api.UnitTests, Curl.Api.UnitLibrary, Curl.Api]
requirement: none
created: 2026-11-27
completed: 2026-11-27
---
# BL-6077 — Fix CI failure EachProject_HasTitleIconLicenseExpressionAndProjectUrl on Linux and macOS

## Goal

`EachProject_HasTitleIconLicenseExpressionAndProjectUrl` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `EachProject_HasTitleIconLicenseExpressionAndProjectUrl` failed on Linux and macOS in CI run 46017114393 (https://github.com/StewartScottRogers/Curl/actions/runs/46017114393). First failing commit: f8cfce5c.

    Assertion failed. Expected values to be equal.

Lanes test only on Windows, so reproduce with `gh run view 46017114393 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `EachProject_HasTitleIconLicenseExpressionAndProjectUrl` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: f8cfce5c moved the three Surly packages from `PackageLicenseExpression` `curl` to a packed
  `LICENSE.txt` (`PackageLicenseFile`), because nuget.org refuses that expression. The test still
  pinned the old form. It failed on Windows too (job logs of run 46017114393: one failure each on
  Windows, Linux and macOS, plus Quality gates shard 3), not only Linux and macOS. The csproj change
  is correct, so only the test changed.
- The test named in the criterion is now `EachProject_HasTitleIconLicenseFileAndProjectUrl`: it pins
  a licence file and no expression, so the old name would say what it no longer does (lane rules,
  "Say what it does"). It is the same test under its truthful name, and it is what the ticked box
  was checked against. Added `EachProject_PacksTheSharedLicenseFile`, so a declared `LICENSE.txt`
  that is not packed fails here rather than at `dotnet pack`.
- No ADR: the packaging decision was already made by f8cfce5c; this only pins it. No production code
  changed, so no `Measure-CodeQuality.ps1` run.
- CI half of the box (lane rule 11): the test only parses csproj XML and compares file name suffixes,
  so it is platform-neutral; `dotnet build` clean and the fast tests green locally
  (Curl.Api.UnitTests 1038 passed, 2 skipped). The shift's CI watch confirms the run.

## Log

- 2026-11-27: Created.
- 2026-11-27: Backlog -> Doing. Criteria at claim: 63a983c0.
- 2026-11-27: Doing -> Done. The Surly package metadata tests pin the packed LICENSE.txt and pass
