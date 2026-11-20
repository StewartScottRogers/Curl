---
id: AF-0893
title: Cgr170 test named Exit26 asserts exit 25
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
key: quality:Curl.Console.UnitTests/Cgr170Tests.cs:RunAsync_UnreadableUploadFileInTheFirstBatch_ReportsEveryTransferWithExit26AndUploadsNothing:name-lies
reproduction: none
task: BL-5581
tasks: BL-5581
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_0456.md
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

- 2026-11-20 | 2026-11-20_0456.md | reproduces: no | Ran the Select-String: no AreEqual(25 in Cgr170Tests.cs. RunAsync_UnreadableUploadFileInTheFirstBatch_ReportsEveryTransferWithExit26AndUploadsNothing now asserts Assert.AreEqual(26, exitCode) and output "0 26 000\n1 26 000\n".

## Log

- 2026-11-19: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_0456.md: the reproduction no longer reproduces.
