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
completed: 2026-10-21
---
# BL-3019 — Cover every branch of the manual page generator's option header and option page readers

## Goal

Every branch of `ManualOptionHeader` and `ManualOptionPage` is covered by a focused test, so those two types reach 100% line and branch coverage.

## Context

Split from BL-2881, after BL-3018 commits the generator. `Curl.Cli.UnitTests` has no `InternalsVisibleTo` from `Curl.Cli.UnitLibrary` (its csproj is outside this task), so tests reach only public types: make `ManualOptionHeader` and `ManualOptionPage` public (they are documented already) and test them directly, as the earlier `ManualOptionIndexTests` does. Read upstream's `scripts/managen` and `scripts/cd2nroff` at tag `curl-8_22_0` where a refusal or warning text must match. Cover each refusal and warning in `ManualOptionHeader` (missing or malformed header fields, unknown keys) and in `ManualOptionPage` (missing sections, example and see-also handling). Measure with `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` and read only the lines for these two files; the other generator types belong to BL-3020 and BL-3021.

## Acceptance criteria

- [x] `ManualOptionHeaderTests` and `ManualOptionPageTests` exist in `Curl.Cli.UnitTests/ManualPages/`, one test per refusal, warning and branch, each named after the behaviour it pins.
- [x] Measure-CodeQuality reports no uncovered line or branch, complexity above 10 or CRAP above 30 in `ManualOptionHeader` or `ManualOptionPage`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Cli.UnitTests`.

## Notes

- Made `ManualOptionHeader` and `ManualOptionPage` public (with doc comments on the header's properties) so `Curl.Cli.UnitTests` can test them directly, as the Context directs.
- Coverage was already 100% except one branch in `UnknownExampleOption` (an unreachable null check from a `Match` property pattern). But the coverage tool counted complexity 12 to 22 for eight methods (`Parse`, `Write`, `Extra`, `Examples`, `SeeAlso`, `Leading`, `Foot`, `UnknownExampleOption`). Refactored them without changing output: `Parse` reads each line in `ReadLine`; `Write` chains `WriteOption`, `WriteDescription` and `WriteFoot` with `??`; the `Multi` lines come from a template table; and the see-also and mutex lists share `JoinNames` ("a", "a and b", "a, b and c"), because both loops built the same join. `ManualPageGeneratorTests` pass unchanged.
- The ASCII layout indents each continuation line with a tab and four spaces. The full ASCII test pins that byte for byte, as the ported generator writes it.
- Measured with `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary`: neither type has a failing member now. `ManualOptionIndex.Add` (complexity 14) still fails the gate and no task names it, so it is filed as BL-3024.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: a2a87d55 dd9dfdb8 48b212cd.
- 2026-10-21: Doing -> Done. ManualOptionHeader and ManualOptionPage are public, tested branch by branch and inside every quality gate
