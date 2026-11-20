---
id: AF-0894
title: Cgr170 test named ReportsExit26 asserts exit 25
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Console.UnitTests/Cgr170Tests.cs:RunAsync_UnreadableUploadFilePastTheFirstBatch_ReportsExit26WhateverFailedBeforeAndStartsNothingMore:name-lies
reproduction: none
task: BL-5582
tasks: BL-5582
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0894 - Cgr170 test named ReportsExit26 asserts exit 25

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/Cgr170Tests.cs:89`: Cgr170 test named ReportsExit26 asserts exit 25. Reappeared; previously AF-0647.

## Evidence

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:89`

The name says ReportsExit26. The body asserts Assert.AreEqual(25, exitCode); and Assert.AreEqual("0 7\n1 7\n2 25\n", StandardOutput); Nothing checks 26.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'Exit26|AreEqual\(25'
```

- Expected: The name and the asserted exit code agree.
- Actual: Line 89 names ReportsExit26; line 98 asserts Assert.AreEqual(25, exitCode);

## Re-audits

## Log

- 2026-11-19: filed proposed.
