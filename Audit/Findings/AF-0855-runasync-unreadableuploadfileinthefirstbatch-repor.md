---
id: AF-0855
title: RunAsync_UnreadableUploadFileInTheFirstBatch_ReportsEveryTransferWithExit26AndUploadsNothing asserts exit 25, not the 26 its name states
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Console.UnitTests/Cgr170Tests.cs:RunAsync_UnreadableUploadFileInTheFirstBatch_ReportsEveryTransferWithExit26AndUploadsNothing:name-lies
reproduction: none
task: BL-5515
tasks: BL-5515
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0855 - RunAsync_UnreadableUploadFileInTheFirstBatch_ReportsEveryTransferWithExit26AndUploadsNothing asserts exit 25, not the 26 its name states

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/Cgr170Tests.cs:46`: RunAsync_UnreadableUploadFileInTheFirstBatch_ReportsEveryTransferWithExit26AndUploadsNothing asserts exit 25, not the 26 its name states. Reappeared; previously AF-0646. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:46`

The name promises exit 26 (CURLE_READ_ERROR). The body asserts the other number: line 53 Assert.AreEqual(25, exitCode); line 54 Assert.AreEqual("0 25 000\n1 25 000\n", StandardOutput); and stderr 'curl: (25) Upload failed'. No assertion mentions 26 or CurlExitCode.ReadError. Find-WeakTests name-lies candidate, filed under Quality.md step 0's rule that an assertion of a different number is the finding.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'Exit26AndUploadsNothing|Assert.AreEqual\(25, exitCode\)'
```

- Expected: The name and the asserted exit code agree (both 26, or the name says 25).
- Actual: Cgr170Tests.cs:46 ...ReportsEveryTransferWithExit26AndUploadsNothing() and Cgr170Tests.cs:53 Assert.AreEqual(25, exitCode);

## Re-audits

## Log

- 2026-11-18: filed proposed.
