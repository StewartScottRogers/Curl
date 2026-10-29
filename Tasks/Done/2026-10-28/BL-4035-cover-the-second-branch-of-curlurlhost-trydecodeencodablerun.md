---
id: BL-4035
title: Cover the second branch of CurlUrlHost.TryDecodeEncodableRune
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitTests/CurlUrlHostTests.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4035 — Cover the second branch of CurlUrlHost.TryDecodeEncodableRune

## Goal

`CurlUrlHost.TryDecodeEncodableRune` (`Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs`) is at 100% branch coverage, so the CI quality job passes for `Curl.Protocol.Abstractions.UnitLibrary`.

## Context

BL-3829's `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` run on 2026-10-28 reported `CurlUrlHost.TryDecodeEncodableRune(ReadOnlySpan<char>, out Rune, out int)` at 100% line and 50% branch coverage (complexity 2). BL-3829 did not change `CurlUrlHost.cs`; the last commit to it was 76d214730 (CGR-191). Add a focused test in `CurlUrlHostTests.cs` that reaches the uncovered branch (likely the decode failure, such as a lone surrogate).

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports no failing member for `CurlUrlHost.TryDecodeEncodableRune`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- The uncovered branch was the decode failure, and no input can reach it: `ToPunycode` hands `ToAsciiLabel` labels of `CurlUrlHostNameMapping.Map`'s output, and `Map` builds that with `EnumerateRunes`, which reads a lone surrogate as `U+FFFD`. A test with a lone surrogate (added) still left the member at 50% branch coverage, so a test alone could not meet the goal.
- Added `Curl.Protocol.Abstractions.UnitLibrary/CurlUrlHost.cs` to `touches` (no task in Doing on origin/work/dark-factory names it; BL-4034 holds only CurlUrlHostNameMappingTable.cs) and removed the dead branch: `EncodeLabel` now enumerates runes, so a lone surrogate arrives as `U+FFFD` and `IsRefusedInLabel` refuses it, and `TryDecodeEncodableRune` is gone. Behaviour is unchanged (same refusals, same 63-code-point limit); no ADR, as nothing curl-visible changed.
- Tests in CurlUrlHostTests.cs: a control character, U+FFFD, a lone low and a lone high surrogate are each an IDN conversion failure, and `ü.example` is `xn--tda.example`.
- Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary: 100% line, 100% branch, 0 failing members (before the fix: TryDecodeEncodableRune at 50% branch). dotnet build clean; fast tests 45228 passed, 313 skipped, 0 failed.


## Log

- 2026-10-28: Created by BL-3829's run.
- 2026-10-28: Backlog -> Doing. Criteria at claim: d65f24ce 779790c0.
- 2026-10-28: Doing -> Done. CurlUrlHost's dead decode-failure branch is gone, so Curl.Protocol.Abstractions.UnitLibrary is at 100% line and branch coverage
