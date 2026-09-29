---
id: BL-927
title: Log TFTP, file and SMB transfer steps to the diagnostic log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-916]
touches: [Curl.Protocol.Tftp.UnitLibrary, Curl.Protocol.Tftp.UnitTests, Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests, Curl.Protocol.Smb.UnitLibrary, Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-927 — Log TFTP, file and SMB transfer steps to the diagnostic log

## Goal

The TFTP, file and SMB handlers write the diagnostic log (components `tftp`, `file`, `smb`) from `ITransferContext.DiagnosticLog` for each transfer step.

## Context

- The rules are BL-915's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-916's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `Curl.Protocol.Tftp.UnitLibrary`: `TftpProtocolHandler.cs`, `TftpDownload.cs`, `TftpRetrySchedule.cs`, `TftpErrorMapping.cs`, `TftpMasqueRequest.cs`/`TftpMasqueReply.cs`; `Curl.Protocol.File.UnitLibrary`: `FileProtocolHandler.cs`; `Curl.Protocol.Smb.UnitLibrary`: `SmbProtocolHandler.cs`, `SmbSessionEstablisher.cs`, `SmbFileTransfer.cs` (both wrap or build a context: forward `DiagnosticLog` and set it on each `ConnectTarget`).
- What, per level: `error` the failure that ends the transfer with its `CurlExitCode` (TFTP error packet code and text, file open failure with the .NET exception, SMB NT status); `warning` a TFTP retransmission, an OACK option the server changed or ignored; `info` TFTP request sent and options agreed (blksize, tsize, timeout), the file path opened and its size, SMB negotiate, session and tree connect done, transfer end with bytes and ms; `verbose` each TFTP packet (opcode, block number, length), each SMB command and status.
- Credential-bearing path: `smb://user:s3cret@host/share/file` (the SMB session setup never logs the password or its hash).

## Acceptance criteria

- [ ] `Curl.Protocol.Tftp.UnitTests` pin a download's agreed options at `info`, a retransmission at `warning` and a TFTP error packet at `error` naming its `CurlExitCode`.
- [ ] `Curl.Protocol.File.UnitTests` pin a file opened at `info` and a missing file at `error` naming `FileCouldntReadFile` (exit 37); paths in tests are drive-less (`file:///dir/x`) so they pass on Linux and macOS.
- [ ] `Curl.Protocol.Smb.UnitTests` pin session and tree connect at `info`, a refused tree connect at `error`, and the no-secret test above.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

## Log

- 2026-09-29: Created.
