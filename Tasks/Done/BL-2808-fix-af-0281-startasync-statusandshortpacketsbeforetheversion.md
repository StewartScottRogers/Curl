---
id: BL-2808
title: Fix AF-0281: StartAsync_StatusAndShortPacketsBeforeTheVersion_SkipsThemAsMeasured has only a log-only diagnostics.Assert
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-19
completed: 2026-10-19
---
# BL-2808 — Fix AF-0281: StartAsync_StatusAndShortPacketsBeforeTheVersion_SkipsThemAsMeasured has only a log-only diagnostics.Assert

## Goal

The defect the audit office reported as AF-0281 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0281 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0281-startasync-statusandshortpacketsbeforetheversion-s.md`.

Location: `Curl.Protocol.Ssh.UnitTests/Sftp/SftpSessionTests.cs:58`

Location: `Curl.Protocol.Ssh.UnitTests/Sftp/SftpSessionTests.cs:58`

The test's only check is Diagnostics.Assert("session started", true, session is not null). TestDiagnostics.Assert writes a log line and never fails, and StartAsync's non-nullable result is never null. A session that took the short Version packet as the server's version (instead of skipping it for the version 3 packet) passes, because neither the version nor the requests written are checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Ssh.UnitTests/Sftp/SftpSessionTests.cs -Pattern 'Diagnostics.Assert\("session started"'
```

- Expected: The test asserts the negotiated version (3) or the bytes written, with a real Assert.
- Actual: Two matches (lines 54 and 71), the only checks of both StartAsync tests.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Both StartAsync tests that ended on the log-only `Diagnostics.Assert("session started", ...)` (AcceptsIt and SkipsThemAsMeasured) now share `AssertStartsThenAnswersRealPathAsync`: the script ends with the `REALPATH .` answer, and the test asserts with real Asserts that exactly a version 3 INIT and the REALPATH were written and the home directory came back. A session that stopped at a skipped Status or short Version packet would read that packet as the REALPATH answer and fail. Test-only change, so no quality run was needed.

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-19: Doing -> Done. SftpSession start tests assert the INIT v3 and REALPATH bytes and the home directory with real Asserts
