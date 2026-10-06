---
id: BL-927
title: Log TFTP, file and SMB transfer steps to the diagnostic log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-938]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-927 — Log TFTP, file and SMB transfer steps to the diagnostic log

## Goal

The TFTP, file and SMB handlers write the diagnostic log (components `tftp`, `file`, `smb`) from `ITransferContext.DiagnosticLog` for each transfer step.

## Context

- The rules are BL-937's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-938's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `Curl.Protocol.Tftp.UnitLibrary`: `TftpProtocolHandler.cs`, `TftpDownload.cs`, `TftpRetrySchedule.cs`, `TftpErrorMapping.cs`, `TftpMasqueRequest.cs`/`TftpMasqueReply.cs`; `Curl.Protocol.File.UnitLibrary`: `FileProtocolHandler.cs`; `Curl.Protocol.Smb.UnitLibrary`: `SmbProtocolHandler.cs`, `SmbSessionEstablisher.cs`, `SmbFileTransfer.cs` (both wrap or build a context: forward `DiagnosticLog` and set it on each `ConnectTarget`).
- What, per level: `error` the failure that ends the transfer with its `CurlExitCode` (TFTP error packet code and text, file open failure with the .NET exception, SMB NT status); `warning` a TFTP retransmission, an OACK option the server changed or ignored; `info` TFTP request sent and options agreed (blksize, tsize, timeout), the file path opened and its size, SMB negotiate, session and tree connect done, transfer end with bytes and ms; `verbose` each TFTP packet (opcode, block number, length), each SMB command and status.
- Credential-bearing path: `smb://user:s3cret@host/share/file` (the SMB session setup never logs the password or its hash).

## Acceptance criteria

- [x] `Curl.Protocol.Tftp.UnitTests` pin a download's agreed options at `info`, a retransmission at `warning` and a TFTP error packet at `error` naming its `CurlExitCode`.
- [x] `Curl.Protocol.File.UnitTests` pin a file opened at `info` and a missing file at `error` naming `FileCouldntReadFile` (exit 37); paths in tests are drive-less (`file:///dir/x`) so they pass on Linux and macOS.
- [x] `Curl.Protocol.Smb.UnitTests` pin session and tree connect at `info`, a refused tree connect at `error`, and the no-secret test above.
- [x] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [x] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

- Shape: one small internal writer per library - `SmbTransferLog`, `FileTransferLog`,
  `TftpTransferLog` - each with one method per step that tests `IsEnabled` before it
  formats, so the handlers only gain one-line calls and every method stays well under
  complexity 10 (worst CRAP 8 to 10). Each handler's `ExecuteAsync` now times the
  transfer with `ITransferContext.TimeProvider` and writes its end: `transfer done: N bytes
  in M ms` at `info`, or `transfer failed with <CurlExitCode> (exit N): <message>` at
  `error`. The same shape is copied, not shared: protocol libraries cannot reference each
  other and `Curl.Protocol.Abstractions.UnitLibrary` is outside this task's `touches`.
- SMB: `SmbMessageReader` logs each complete reply's command name and NT status at
  `verbose`; `SmbSessionEstablisher` and `SmbFileTransfer` log negotiate, session setup
  (UID only, never the user's password or the NTLM responses), tree connect (share and
  TID) and open (size) at `info`, and a refused negotiate, session setup, tree connect or
  open with its NT status at `error`. Both new constructor parameters are optional, so the
  existing tests build the classes unchanged. The handler sets `ConnectTarget.DiagnosticLog`.
- TFTP: the download logs its read request and options and the OACK's options at `info`,
  each packet at `verbose`, a retransmission and a `blksize` the server changed or ignored
  at `warning`, and an ERROR packet's code and text at `error`. The upload is covered by
  the handler's transfer-end line only; the criteria ask for the download. The MASQUE
  proxy target gets `DiagnosticLog`; the proxy request (which may carry credentials) is
  never logged. Added `InternalsVisibleTo` for `Curl.Protocol.Tftp.UnitTests` so
  `TftpTransferLog` can be tested directly, as the SMB and file libraries already allow.
- File: the .NET exception of a failed open never reaches the handler (`IFileSystem`
  returns only a `FileAccessStatus`), so the `error` line names the status
  (`could not open /dir/x for reading: NotFound`) and the handler's end line names
  `FileCouldntReadFile (exit 37)`. Carrying the exception needs `FileOpenResult` and
  `PhysicalFileSystem` changes outside `touches`: filed as BL-967. `TimeProvider` is now
  read by the file handler to time the end line; its XML remarks and `CLAUDE.md` table say so.
- No `-v`, `--trace`, standard output or exit-code change: every existing test in the three
  test projects passes unmodified (104 SMB, 313 file, 136 TFTP, new tests included).
- Measured: `Measure-CodeQuality.ps1 -Library` reports 100% line, 100% branch and 0 failing
  members for all three libraries.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. TFTP, file and SMB handlers write their transfer steps to the diagnostic log under tftp, file and smb
