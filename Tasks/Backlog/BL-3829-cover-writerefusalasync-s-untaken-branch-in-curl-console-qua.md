---
id: BL-3829
title: Cover WriteRefusalAsync's untaken branch in Curl.Console (quality gate: branch 75%)
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console.UnitTests/CurlCommandRunnerWriteRefusalTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3829 — Cover WriteRefusalAsync's untaken branch in Curl.Console (quality gate: branch 75%)

## Goal

`CurlCommandRunner.WriteRefusalAsync` (Curl.Console/CurlCommandRunner.cs, about line 5160) meets the 100% branch gate.

## Context

BL-3555's `Measure-CodeQuality.ps1 -Library Curl.Console` run on 2026-10-26 reported it as the one failing member in Curl.Console: line 100%, branch 75%, Cx 4. BL-3555 did not change it (last touched by 6b44942d2). Find the untaken branch from a coverage report and add a unit test that takes it.

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console` no longer lists `CurlCommandRunner.WriteRefusalAsync`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
