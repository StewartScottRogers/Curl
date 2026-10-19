---
id: AF-0281
title: StartAsync_StatusAndShortPacketsBeforeTheVersion_SkipsThemAsMeasured has only a log-only diagnostics.Assert
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Protocol.Ssh.UnitTests/Sftp/SftpSessionTests.cs:StartAsync_StatusAndShortPacketsBeforeTheVersion_SkipsThemAsMeasured:no-assertion
reproduction: none
task: none
tasks:
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0281 - StartAsync_StatusAndShortPacketsBeforeTheVersion_SkipsThemAsMeasured has only a log-only diagnostics.Assert

## Summary

Low finding from the quality auditor at `Curl.Protocol.Ssh.UnitTests/Sftp/SftpSessionTests.cs:58`: StartAsync_StatusAndShortPacketsBeforeTheVersion_SkipsThemAsMeasured has only a log-only diagnostics.Assert. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Protocol.Ssh.UnitTests/Sftp/SftpSessionTests.cs:58`

The test's only check is Diagnostics.Assert("session started", true, session is not null). TestDiagnostics.Assert writes a log line and never fails, and StartAsync's non-nullable result is never null. A session that took the short Version packet as the server's version (instead of skipping it for the version 3 packet) passes, because neither the version nor the requests written are checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ssh.UnitTests/Sftp/SftpSessionTests.cs -Pattern 'Diagnostics.Assert\("session started"'
```

- Expected: The test asserts the negotiated version (3) or the bytes written, with a real Assert.
- Actual: Two matches (lines 54 and 71), the only checks of both StartAsync tests.

## Re-audits

## Log

- 2026-10-18: filed proposed.
