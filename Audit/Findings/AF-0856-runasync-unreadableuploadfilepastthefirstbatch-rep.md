---
id: AF-0856
title: RunAsync_UnreadableUploadFilePastTheFirstBatch_ReportsExit26WhateverFailedBeforeAndStartsNothingMore asserts exit 25, not 26
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
key: quality:Curl.Console.UnitTests/Cgr170Tests.cs:RunAsync_UnreadableUploadFilePastTheFirstBatch_ReportsExit26WhateverFailedBeforeAndStartsNothingMore:name-lies
reproduction: none
task: BL-5498
tasks: BL-5498
found: 2026-11-18
found-at: 4f902456d88c3fda856546dbe0c9a7674b1f04c4
scorecard: 2026-11-18_0810.md
duplicate-of:
closed: 2026-11-19
closed-how: reliable-reaudit
closed-by: 2026-11-19_0134.md
---
# AF-0856 - RunAsync_UnreadableUploadFilePastTheFirstBatch_ReportsExit26WhateverFailedBeforeAndStartsNothingMore asserts exit 25, not 26

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/Cgr170Tests.cs:89`: RunAsync_UnreadableUploadFilePastTheFirstBatch_ReportsExit26WhateverFailedBeforeAndStartsNothingMore asserts exit 25, not 26. Reappeared; previously AF-0647. Reported by an auditor flagged unreliable in 2026-11-18_0810.md.

## Evidence

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:89`

The name promises exit 26. The body asserts line 98 Assert.AreEqual(25, exitCode); and line 99 stdout "0 7\n1 7\n2 25\n". Nothing checks 26 or CurlExitCode.ReadError.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'ReportsExit26WhateverFailedBefore|Assert.AreEqual\(25, exitCode\)'
```

- Expected: The name and the asserted exit code agree.
- Actual: Cgr170Tests.cs:89 ...ReportsExit26WhateverFailedBeforeAndStartsNothingMore() and Cgr170Tests.cs:98 Assert.AreEqual(25, exitCode);

## Re-audits

- 2026-11-19 | 2026-11-19_0134.md | reproduces: no | Only the test name matched (line 89); there is no 'Assert.AreEqual(25, exitCode)'. The test asserts Assert.AreEqual(26, exitCode) at line 98.

## Log

- 2026-11-18: filed proposed.
- 2026-11-19: accepted -> closed. Re-audit 2026-11-19_0134.md: the reproduction no longer reproduces.
