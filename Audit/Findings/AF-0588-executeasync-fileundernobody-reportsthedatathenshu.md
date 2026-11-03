---
id: AF-0588
title: ExecuteAsync_FileUnderNoBody_ReportsTheDataThenShuttingDownWithNoLineForExit8 never checks exit 8
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Original.Protocol.Smb.UnitTests/SmbProtocolHandlerVerboseTests.cs:ExecuteAsync_FileUnderNoBody_ReportsTheDataThenShuttingDownWithNoLineForExit8:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0588 - ExecuteAsync_FileUnderNoBody_ReportsTheDataThenShuttingDownWithNoLineForExit8 never checks exit 8

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Smb.UnitTests/SmbProtocolHandlerVerboseTests.cs:110`: ExecuteAsync_FileUnderNoBody_ReportsTheDataThenShuttingDownWithNoLineForExit8 never checks exit 8. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Original.Protocol.Smb.UnitTests/SmbProtocolHandlerVerboseTests.cs:110`

Step 0 name-lies. The test keeps only the transcript and asserts CollectionAssert.AreEqual(new[] { "<= hello world", "* shutting down connection #0" }, transcript); the transfer's exit code 8 is never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Smb.UnitTests/SmbProtocolHandlerVerboseTests.cs -Pattern 'NoLineForExit8' -Context 0,6
```

- Expected: The body asserts the result's ExitCode is WeirdServerReply (8).
- Actual: Only the transcript is asserted.

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
