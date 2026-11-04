---
id: AF-0580
title: WebDavList ExecuteAsync_ASuccessWithNoReport_FailsWithExit22 never checks exit 22
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavListTransferHookTests.cs:ExecuteAsync_ASuccessWithNoReport_FailsWithExit22:name-lies
reproduction: none
task: BL-4744
tasks: BL-4744
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0580 - WebDavList ExecuteAsync_ASuccessWithNoReport_FailsWithExit22 never checks exit 22

## Summary

High finding from the quality auditor at `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavListTransferHookTests.cs:62`: WebDavList ExecuteAsync_ASuccessWithNoReport_FailsWithExit22 never checks exit 22. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavListTransferHookTests.cs:62`

Step 0 name-lies. The only assertion is Assert.AreEqual("The server answered PROPFIND with 0, not 207 Multistatus", result.ErrorMessage); result.ExitCode (HttpReturnedError, 22) is not checked, though line 56 of the same file shows the check.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Extension.WebDavTools.UnitTests/Transfer/WebDavListTransferHookTests.cs -Pattern 'ASuccessWithNoReport_FailsWithExit22' -Context 0,5
```

- Expected: The body asserts Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode).
- Actual: Only result.ErrorMessage is asserted.

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Asserts Assert.AreEqual(CurlExitCode.HttpReturnedError, result.ExitCode) (22) and the message.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
