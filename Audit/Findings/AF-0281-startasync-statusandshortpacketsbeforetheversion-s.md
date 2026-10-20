---
id: AF-0281
title: StartAsync_StatusAndShortPacketsBeforeTheVersion_SkipsThemAsMeasured has only a log-only diagnostics.Assert
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
key: quality:Curl.Protocol.Ssh.UnitTests/Sftp/SftpSessionTests.cs:StartAsync_StatusAndShortPacketsBeforeTheVersion_SkipsThemAsMeasured:no-assertion
reproduction: none
task: BL-2808
tasks: BL-2808
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed: 2026-10-19
closed-how: reliable-reaudit
closed-by: 2026-10-19_2134.md
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

- 2026-10-19 | 2026-10-19_2134.md | reproduces: no | Select-String found no Diagnostics.Assert("session started" line in SftpSessionTests.cs. The test now calls AssertStartsThenAnswersRealPathAsync(script), a real assertion helper.

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
- 2026-10-19: accepted -> closed. Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces.
