---
id: AF-0247
title: TestOneAsync_TransferHangs_RunsForever accepts any non-Ok result instead of LibTestErrorCodes.RunsForever
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.LibTest.UnitTests/Lib530Tests.cs:TestOneAsync_TransferHangs_RunsForever:name-lies
reproduction: none
task: BL-2758
tasks: BL-2758
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0247 - TestOneAsync_TransferHangs_RunsForever accepts any non-Ok result instead of LibTestErrorCodes.RunsForever

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitTests/Lib530Tests.cs:183`: TestOneAsync_TransferHangs_RunsForever accepts any non-Ok result instead of LibTestErrorCodes.RunsForever. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Conformance.LibTest.UnitTests/Lib530Tests.cs:183`

Step 1 sample. The name promises the RunsForever outcome, and LibTestErrorCodes.RunsForever exists (LibTestErrorCodes.cs:16) and is asserted exactly by other ports' tests (e.g. Lib1525To1534Tests.cs:216). This test's only check is `Assert.AreNotEqual(CurlCode.Ok, result);`. If Lib530 returned MajorBad, Select or any other failure code for a hang, the test would still pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib530Tests.cs -Pattern 'Assert.AreNotEqual\(CurlCode.Ok, result\)'
```

- Expected: No match: the test asserts Assert.AreEqual(LibTestErrorCodes.RunsForever, result).
- Actual: Lib530Tests.cs:183: Assert.AreNotEqual(CurlCode.Ok, result);

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
