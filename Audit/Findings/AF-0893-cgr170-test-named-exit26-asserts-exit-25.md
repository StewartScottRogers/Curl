---
id: AF-0893
title: Cgr170 test named Exit26 asserts exit 25
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Console.UnitTests/Cgr170Tests.cs:RunAsync_UnreadableUploadFileInTheFirstBatch_ReportsEveryTransferWithExit26AndUploadsNothing:name-lies
reproduction: none
task: BL-5581
tasks: BL-5581
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0893 - Cgr170 test named Exit26 asserts exit 25

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/Cgr170Tests.cs:46`: Cgr170 test named Exit26 asserts exit 25. Reappeared; previously AF-0646.

## Evidence

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:46`

The name says ReportsEveryTransferWithExit26. The body asserts Assert.AreEqual(25, exitCode); and Assert.AreEqual("0 25 000\n1 25 000\n", StandardOutput); with standard error 'curl: (25) Upload failed'. Nothing checks 26.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'Exit26|AreEqual\(25'
```

- Expected: The name and the asserted exit code agree.
- Actual: Line 46 names Exit26; line 53 asserts Assert.AreEqual(25, exitCode);

## Re-audits

## Log

- 2026-11-19: filed proposed.
