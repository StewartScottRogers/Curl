---
id: BL-2758
title: Fix AF-0247: TestOneAsync_TransferHangs_RunsForever accepts any non-Ok result instead of LibTestErrorCodes.RunsForever
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2758 — Fix AF-0247: TestOneAsync_TransferHangs_RunsForever accepts any non-Ok result instead of LibTestErrorCodes.RunsForever

## Goal

The defect the audit office reported as AF-0247 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0247 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0247-testoneasync-transferhangs-runsforever-accepts-any.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib530Tests.cs:183`

Location: `Curl.Conformance.LibTest.UnitTests/Lib530Tests.cs:183`

Step 1 sample. The name promises the RunsForever outcome, and LibTestErrorCodes.RunsForever exists (LibTestErrorCodes.cs:16) and is asserted exactly by other ports' tests (e.g. Lib1525To1534Tests.cs:216). This test's only check is `Assert.AreNotEqual(CurlCode.Ok, result);`. If Lib530 returned MajorBad, Select or any other failure code for a hang, the test would still pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib530Tests.cs -Pattern 'Assert.AreNotEqual\(CurlCode.Ok, result\)'
```

- Expected: No match: the test asserts Assert.AreEqual(LibTestErrorCodes.RunsForever, result).
- Actual: Lib530Tests.cs:183: Assert.AreNotEqual(CurlCode.Ok, result);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
