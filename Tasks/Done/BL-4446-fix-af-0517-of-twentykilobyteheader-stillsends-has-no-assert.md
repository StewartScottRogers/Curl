---
id: BL-4446
title: Fix AF-0517: Of_TwentyKilobyteHeader_StillSends has no assertion; its field count goes only to the diagnostics log
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitTests]
requirement: none
created: 2026-11-01
completed: 2026-11-01
---
# BL-4446 — Fix AF-0517: Of_TwentyKilobyteHeader_StillSends has no assertion; its field count goes only to the diagnostics log

## Goal

The defect the audit office reported as AF-0517 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0517 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0517-of-twentykilobyteheader-stillsends-has-no-assertio.md`.

Location: `Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs:35`

Location: `Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs:35`

The test's only check is Diagnostics.Assert("field count", 5, Http2RequestHeaders.Of(...).Count) at line 35, which logs and cannot fail (TestDiagnostics.cs:189-193). Any field count passes; only an exception would fail it.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Http.UnitTests/Cgr271Tests.cs -Pattern 'Of_TwentyKilobyteHeader_StillSends' -Context 0,6
```

- Expected: The body ends in Assert.AreEqual(5, ...Count) or equivalent.
- Actual: The body ends in Diagnostics.Assert("field count", 5, ...Count); no Assert.* call.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-01: Already fixed by 2a9ab9df6 (AF-0516's task), which added `Assert.AreEqual(5, fieldCount)` after the `Diagnostics.Assert` line in Of_TwentyKilobyteHeader_StillSends; no code change was needed here. Ran the reproduction: the body now holds an Assert.AreEqual on the field count. dotnet build clean (0 errors) and the fast tests green (0 failed projects). The test change is platform-neutral (string bytes only).

## Log

- 2026-11-01: Created.
- 2026-11-01: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-01: Doing -> Done. Of_TwentyKilobyteHeader_StillSends asserts its field count with Assert.AreEqual (landed in 2a9ab9df6)
