---
id: BL-4755
title: Fix AF-0591: Surviving mutant: a gRPC-Web trailer line starting with ':' can be refused as having no colon without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.GrpcWeb.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4755 — Fix AF-0591: Surviving mutant: a gRPC-Web trailer line starting with ':' can be refused as having no colon without a test failing

## Goal

The defect the audit office reported as AF-0591 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0591 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0591-surviving-mutant-a-grpc-web-trailer-line-starting.md`.

Location: `Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebTrailers.cs:27`

Location: `Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebTrailers.cs:27`

Drawn library (seed 985570). if (colon < 0) -> if (colon <= 0) survived: a trailer line ':x' is then refused with FormatException instead of read as a field with an empty name. GrpcWebTrailersTests covers only a line with no colon at all.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebTrailers.cs:27:< -Member Parse -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `GrpcWebTrailersTests.Parse_ALineStartingWithAColon_GivesAFieldWithAnEmptyName`: `":x"` reads as the field (`""`, `"x"`). Test only; production code unchanged, so no quality run was needed.
- A lane may not run `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand instead: with `if (colon <= 0)` at `GrpcWebTrailers.cs:27` the new test fails (1 of 34), with the original code all 34 pass. The re-audit confirms the reproduction.
- Build clean (0 warnings). Fast tests: all green except `MissedPing_EndsTheConnectionAfterPingIntervalPlusPingTimeoutOnTheInjectedClock` in `Curl.Conformance.Extended.UnitTests`, which failed once in the full run and passed alone; it is unrelated to this change and is filed as BL-4780.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. A trailer line starting with ':' is pinned as an empty-named field, killing the AF-0591 mutant
