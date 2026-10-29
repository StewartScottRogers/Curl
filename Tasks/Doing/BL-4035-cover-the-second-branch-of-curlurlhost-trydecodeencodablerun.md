---
id: BL-4035
title: Cover the second branch of CurlUrlHost.TryDecodeEncodableRune
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitTests/CurlUrlHostTests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-4035 — Cover the second branch of CurlUrlHost.TryDecodeEncodableRune

## Goal

`CurlUrlHost.TryDecodeEncodableRune` (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs`) is at 100% branch coverage, so the CI quality job passes for `Curl.Protocol.Abstractions.UnitLibrary`.

## Context

BL-3829's `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` run on 2026-10-28 reported `CurlUrlHost.TryDecodeEncodableRune(ReadOnlySpan<char>, out Rune, out int)` at 100% line and 50% branch coverage (complexity 2). BL-3829 did not change `CurlUrlHost.cs`; the last commit to it was 76d214730 (CGR-191). Add a focused test in `CurlUrlHostTests.cs` that reaches the uncovered branch (likely the decode failure, such as a lone surrogate).

## Acceptance criteria

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports no failing member for `CurlUrlHost.TryDecodeEncodableRune`.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-28: Created by BL-3829's run.
- 2026-10-28: Backlog -> Doing. Criteria at claim: d65f24ce 779790c0.
