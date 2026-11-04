---
id: BL-4697
title: Fix AF-0549: Lib1678 checks the shared session cache only for null
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4697 — Fix AF-0549: Lib1678 checks the shared session cache only for null

## Goal

The defect the audit office reported as AF-0549 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0549 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0549-lib1678-checks-the-shared-session-cache-only-for-n.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:134`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs:134`

'Assert.IsNotNull(engine.LastShare);' is the only check of the 'shared session cache' the name promises; a share that shares nothing, or no SSL sessions, passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1649To1906Tests.cs -Pattern 'Assert.IsNotNull\(engine.LastShare\)' | Select-Object -First 1
```

- Expected: An assertion that the share shares SSL sessions.
- Actual: Lib1649To1906Tests.cs:134: Assert.IsNotNull(engine.LastShare);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Pipeline `feature` run as a direct test-only change: `FakeShareHandle` now records each `Share` call in `Shared`; Lib1678 asserts exactly `[SslSession]` was shared, and Lib1905 (the only other `Assert.IsNotNull(engine.LastShare)` in the file, which the reproduction's `-First 1` would otherwise hit next) asserts `[Cookie, Cookie]`, as its program shares cookies twice.
- Reproduction now prints nothing. `dotnet build` clean; fast tests green (Curl.Conformance.LibTest.UnitTests 1189 passed). No production code changed, so no quality run. Platform-neutral: no path, OS error or certificate.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. Lib1678 asserts the share shares SSL sessions only; Lib1905 asserts it shares cookies
