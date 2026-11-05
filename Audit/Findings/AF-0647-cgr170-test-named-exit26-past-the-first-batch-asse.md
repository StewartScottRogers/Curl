---
id: AF-0647
title: Cgr170 test named Exit26 (past the first batch) asserts exit 25
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-05_0819.md: the reproduction no longer reproduces.
key: quality:Curl.Console.UnitTests/Cgr170Tests.cs:RunAsync_UnreadableUploadFilePastTheFirstBatch_ReportsExit26WhateverFailedBeforeAndStartsNothingMore:name-lies
reproduction: none
task: BL-4829
tasks: BL-4829
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed: 2026-11-05
closed-how: reliable-reaudit
closed-by: 2026-11-05_0819.md
---
# AF-0647 - Cgr170 test named Exit26 (past the first batch) asserts exit 25

## Summary

High finding from the quality auditor at `Curl.Console.UnitTests/Cgr170Tests.cs:89`: Cgr170 test named Exit26 (past the first batch) asserts exit 25.

## Evidence

Location: `Curl.Console.UnitTests/Cgr170Tests.cs:89`

The name promises exit 26. The body asserts Assert.AreEqual(25, exitCode) and stdout '0 7 / 1 7 / 2 25'.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Console.UnitTests/Cgr170Tests.cs -Pattern 'ReportsExit26WhateverFailedBefore' -Context 0,12
```

- Expected: The name and the asserted exit code agree.
- Actual: Name says Exit26; body: Assert.AreEqual(25, exitCode);

## Re-audits

- 2026-11-04 | 2026-11-04_1442.md | reproduces: no | Ran the Select-String: Cgr170Tests.cs:98 Assert.AreEqual(26, exitCode); line 99 expects '0 7\n1 7\n2 26\n'. Matches the name's Exit26.
- 2026-11-05 | 2026-11-05_0819.md | reproduces: no | Ran the reproduction: Cgr170Tests.cs:98 is 'Assert.AreEqual(26, exitCode);' and :99 asserts '0 7\n1 7\n2 26\n'.

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
- 2026-11-05: accepted -> closed. Re-audit 2026-11-05_0819.md: the reproduction no longer reproduces.
