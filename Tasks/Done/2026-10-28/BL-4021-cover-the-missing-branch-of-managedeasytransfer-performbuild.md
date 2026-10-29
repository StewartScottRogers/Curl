---
id: BL-4021
title: Cover the missing branch of ManagedEasyTransfer.PerformBuildingMimePostAsync
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/ManagedEasyTransferTests.Mime.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4021 — Cover the missing branch of ManagedEasyTransfer.PerformBuildingMimePostAsync

## Goal

`ManagedEasyTransfer.PerformBuildingMimePostAsync` (`Curl.Engine.UnitLibrary/ManagedEasyTransfer.PostBody.cs:43`) is at 100% branch coverage, so the CI quality job passes for `Curl.Engine.UnitLibrary`.

## Context

BL-4015's `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` run (2026-10-28) reported it at 100% line, 90% branch, complexity 10: one branch no fast test reaches. The member was not changed by BL-4015; the gap was already on the branch.

## Acceptance criteria

- [x] A unit test in `Curl.Engine.UnitTests/ManagedEasyTransferTests.Mime.cs` reaches the uncovered branch of `PerformBuildingMimePostAsync`.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` no longer lists `PerformBuildingMimePostAsync`.
- [x] `dotnet build` is clean and the fast tests of `Curl.Engine.UnitTests` pass.

## Notes

- The uncovered branch was `form is { IsBuilt: false }` being true: no engine test made a MIME post fail to build. `PerformAsync_MimePostThatFailsToBuild_EndsWithTheBuildFailureAndSendsNothing` builds a part named `a
b` under `MimeOptions.FormEscape`, which `MultipartFormBodyBuilder` refuses with exit 43 before anything is sent.
- `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` (2026-10-28): 0 failing members; `PerformBuildingMimePostAsync` no longer listed. `dotnet build` clean; `Curl.Engine.UnitTests` fast tests 2759 passed, 7 skipped. Test only, platform-neutral.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: b8ad5499 277d69a5 ca28a660.
- 2026-10-28: Doing -> Done. PerformBuildingMimePostAsync's build-failure branch is covered; Curl.Engine.UnitLibrary has no failing member
