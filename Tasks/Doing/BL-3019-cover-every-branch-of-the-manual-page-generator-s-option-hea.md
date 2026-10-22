---
id: BL-3019
title: Cover every branch of the manual page generator's option header and option page readers
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3018]
touches: [Curl.Cli.UnitLibrary/ManualPages/ManualOptionHeader.cs, Curl.Cli.UnitLibrary/ManualPages/ManualOptionPage.cs, Curl.Cli.UnitTests/ManualPages/ManualOptionHeaderTests.cs, Curl.Cli.UnitTests/ManualPages/ManualOptionPageTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-3019 — Cover every branch of the manual page generator's option header and option page readers

## Goal

Every branch of `ManualOptionHeader` and `ManualOptionPage` is covered by a focused test, so those two types reach 100% line and branch coverage.

## Context

Split from BL-2881, after BL-3018 commits the generator. `Curl.Cli.UnitTests` has no `InternalsVisibleTo` from `Curl.Cli.UnitLibrary` (its csproj is outside this task), so tests reach only public types: make `ManualOptionHeader` and `ManualOptionPage` public (they are documented already) and test them directly, as the earlier `ManualOptionIndexTests` does. Read upstream's `scripts/managen` and `scripts/cd2nroff` at tag `curl-8_22_0` where a refusal or warning text must match. Cover each refusal and warning in `ManualOptionHeader` (missing or malformed header fields, unknown keys) and in `ManualOptionPage` (missing sections, example and see-also handling). Measure with `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` and read only the lines for these two files; the other generator types belong to BL-3020 and BL-3021.

## Acceptance criteria

- [ ] `ManualOptionHeaderTests` and `ManualOptionPageTests` exist in `Curl.Cli.UnitTests/ManualPages/`, one test per refusal, warning and branch, each named after the behaviour it pins.
- [ ] Measure-CodeQuality reports no uncovered line or branch, complexity above 10 or CRAP above 30 in `ManualOptionHeader` or `ManualOptionPage`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Cli.UnitTests`.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: a2a87d55 dd9dfdb8 48b212cd.
