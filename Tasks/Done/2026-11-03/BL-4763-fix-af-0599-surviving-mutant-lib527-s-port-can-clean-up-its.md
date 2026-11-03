---
id: BL-4763
title: Fix AF-0599: Surviving mutant: lib527's port can clean up its handles on the opposite outcome without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitLibrary]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4763 — Fix AF-0599: Surviving mutant: lib527's port can clean up its handles on the opposite outcome without a test failing

## Goal

The defect the audit office reported as AF-0599 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0599 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0599-surviving-mutant-lib527-s-port-can-clean-up-its-ha.md`.

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147`

steps.Result != CurlCode.Ok -> == survived for testNumber 527: the port then cleans up its handles after success and leaves them after failure, the reverse of upstream lib526.c's TEST_HANG/527 branch.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib526.cs:147:!= -Member CleansUpTheHandles -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- `RunAsync_ASecondAddThatFails_FailsWithTestErrMultiAndCleansUpEveryEasyHandle` (Curl.Conformance.LibTest.UnitTests/Lib525To533Tests.cs) now asserts every easy handle is disposed when the second add fails. For 527 the port disposes handle 0 as it advances and the failure cleanup disposes the other two, so the `!=` -> `==` mutant leaves two undisposed and the test fails; on success every 527 handle is already disposed and nulled, so the mutant is equivalent there. Test-only change: no production code changed, so no quality run was needed.
- The reproduction uses `Audit/Tools/Invoke-MutationTest.ps1`, which a lane may not read or run; the kill was checked by reading the code (a manual mutant run was refused by the sandbox). The re-audit confirms it.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. lib527's cleanup branch is pinned: a failing second add must dispose every easy handle, killing the != to == mutant
