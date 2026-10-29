---
id: BL-4017
title: Cover the missing branch of ManagedEasyTransfer.PerformBuildingMimePostAsync
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/ManagedEasyTransferTests.Mime.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4017 — Cover the missing branch of ManagedEasyTransfer.PerformBuildingMimePostAsync

## Goal

`ManagedEasyTransfer.PerformBuildingMimePostAsync` (`Curl.Engine.UnitLibrary/ManagedEasyTransfer.PostBody.cs:43`) is at 100% branch coverage, so the CI quality job passes for `Curl.Engine.UnitLibrary`.

## Context

BL-4015's `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` run (2026-10-28) reported it at 100% line, 90% branch, complexity 10: one branch no fast test reaches. The member was not changed by BL-4015; the gap was already on the branch.

## Acceptance criteria

- [ ] A unit test in `Curl.Engine.UnitTests/ManagedEasyTransferTests.Mime.cs` reaches the uncovered branch of `PerformBuildingMimePostAsync`.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` no longer lists `PerformBuildingMimePostAsync`.
- [ ] `dotnet build` is clean and the fast tests of `Curl.Engine.UnitTests` pass.

## Notes

## Log

- 2026-10-28: Created.
