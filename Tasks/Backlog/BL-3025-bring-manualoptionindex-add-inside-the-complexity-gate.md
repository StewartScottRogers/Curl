---
id: BL-3025
title: Bring ManualOptionIndex.Add inside the complexity gate
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary/ManualPages/ManualOptionIndex.cs, Curl.Cli.UnitTests/ManualPages/ManualOptionIndexTests.cs]
requirement: none
created: 2026-10-21
completed:
---
# BL-3025 — Bring ManualOptionIndex.Add inside the complexity gate

## Goal

`ManualOptionIndex.Add` passes every quality gate, without any change to what it records or warns.

## Context

Found by BL-3019: `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports `ManualOptionIndex.Add` (`Curl.Cli.UnitLibrary/ManualPages/ManualOptionIndex.cs:30`) at complexity 14, above the gate of 10, and no task names it (BL-3020 and BL-3021 cover other generator types). Split the header loop as BL-3019 split `ManualOptionHeader.Parse`, moving each line into its own method. `ManualOptionIndexTests` already pins the behaviour.

## Acceptance criteria

- [ ] Measure-CodeQuality reports no uncovered line or branch, complexity above 10 or CRAP above 30 in `ManualOptionIndex`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes for `Curl.Cli.UnitTests`.

## Notes

## Log

- 2026-10-21: Created.
