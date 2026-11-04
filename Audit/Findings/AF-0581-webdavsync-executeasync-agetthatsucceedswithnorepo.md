---
id: AF-0581
title: WebDavSync ExecuteAsync_AGetThatSucceedsWithNoReport_FailsWithExit22 never checks exit 22
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-03_2203.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1615.md, 2026-11-03_2203.md).
key: quality:Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs:ExecuteAsync_AGetThatSucceedsWithNoReport_FailsWithExit22:name-lies
reproduction: none
task: BL-4745
tasks: BL-4745
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: consecutive
closed-by: 2026-11-03_1615.md, 2026-11-03_2203.md
---
# AF-0581 - WebDavSync ExecuteAsync_AGetThatSucceedsWithNoReport_FailsWithExit22 never checks exit 22

## Summary

High finding from the quality auditor at `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs:91`: WebDavSync ExecuteAsync_AGetThatSucceedsWithNoReport_FailsWithExit22 never checks exit 22. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs:91`

Step 0 name-lies. The only assertion is Assert.AreEqual("The server answered GET /dav/a.txt with 0", result.ErrorMessage); result.ExitCode is never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavSyncTransferHookTests.cs -Pattern 'AGetThatSucceedsWithNoReport_FailsWithExit22' -Context 0,10
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode).
- Actual: Only result.ErrorMessage is asserted.

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Asserts Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode) (22) and the message.
- 2026-11-03 | 2026-11-03_2203.md | reproduces: no | Ran the reproduction: line 100 Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode) (22).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_2203.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1615.md, 2026-11-03_2203.md).
