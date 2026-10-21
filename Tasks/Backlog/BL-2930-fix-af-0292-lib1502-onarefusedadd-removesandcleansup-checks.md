---
id: BL-2930
title: Fix AF-0292: Lib1502_OnARefusedAdd_RemovesAndCleansUp checks only the exit code, neither the removal nor the clean-up
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2930 — Fix AF-0292: Lib1502_OnARefusedAdd_RemovesAndCleansUp checks only the exit code, neither the removal nor the clean-up

## Goal

The defect the audit office reported as AF-0292 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0292 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0292-lib1502-onarefusedadd-removesandcleansup-checks-on.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs:292`

Location: `Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs:292`

Step 1 sample. With engine.Multi.AddResult = BadEasyHandle, the only assertion is Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode);. FakeCurlEngine exposes DisposedEasyCount and FakeMultiTransfer exposes Removals, and Lib1502.cs:70-79 does DisposeAsync(handle) and multi?.RemoveHandle(easy). The test checks neither, so a port that skipped the clean-up it names would pass. This is the same pattern as AF-0283, in a different test.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib1485To1511Tests.cs -Pattern 'Lib1502_OnARefusedAdd_RemovesAndCleansUp' -Context 0,9
```

- Expected: The body asserts engine.DisposedEasyCount and engine.Multi.Removals as well as the exit code.
- Actual: The body's only assertion is Assert.AreEqual((int)LibTestErrorCodes.Multi, result.ExitCode).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
