---
id: BL-924
title: Log FTP and FTPS session steps to the diagnostic log in Curl.Protocol.Ftp
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-938]
touches: [Curl.Protocol.Ftp.UnitLibrary, Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-924 — Log FTP and FTPS session steps to the diagnostic log in Curl.Protocol.Ftp

## Goal

`Curl.Protocol.Ftp.UnitLibrary` writes the diagnostic log (component `ftp`) from `ITransferContext.DiagnosticLog` for each FTP and FTPS session step: login, TLS upgrade, directory walk, data connection mode, transfer and quit.

## Context

- The rules are BL-937's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-938's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `FtpProtocolHandler.cs` (the handler builds its own `TransferContext`/`ConnectTarget` near lines 205 and, in `FtpSession.cs`, 976: set `DiagnosticLog` on each from the incoming context so the control and data connections log through `Curl.Networking`), `FtpSession.cs` (each state step), `FtpControlChannel.cs` (each command sent and reply code received), `FtpSessionConnections.cs` (passive or active, EPSV/PASV/EPRT/PORT chosen and fallbacks), `FtpTlsRequirements.cs` (AUTH TLS, PROT, CCC decisions), `FtpUrlPath.cs` and the `--ftp-method` walk, `FtpQuoteCommands.cs`.
- What, per level: `error` the reply or failure that ends the session with its `CurlExitCode`; `warning` a fallback (EPSV refused then PASV, EPRT refused then PORT), a `--ftp-skip-pasv-ip` override, a failed quote marked `*` and skipped; `info` logged in, TLS on the control and data channel, directory reached, transfer started and finished with bytes and ms; `verbose` each command (by verb, with its argument except for `PASS` and `ACCT`) and each reply code and first line.
- Credential-bearing paths: `ftp://user:s3cret@host/` and `--ftp-account s3cret`: the `PASS` and `ACCT` arguments are never logged.

## Acceptance criteria

- [x] `Curl.Protocol.Ftp.UnitTests` pin: a passive download logs `info` for login, the data connection and the transfer end; an EPSV refusal then PASV logs `warning`; at `verbose` a `USER` command appears and no message contains the password; a `550` on `RETR` logs `error` naming its `CurlExitCode`; the data connection's `ConnectTarget` carries the incoming `DiagnosticLog`.
- [x] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [x] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

- BL-930 and BL-931 (curl's own FTP `-v`/`--trace` lines) touch the same library; whichever lands second keeps the other's tests green.
- Delivered (2026-09-29): `FtpDiagnosticLog` wraps `ITransferContext.DiagnosticLog` with one small method per session step, each testing `IsEnabled` before formatting, so `FtpSession` gains only one-line calls and no method passes complexity 10. `FtpControlChannel` logs each command sent and each reply (code and first line) at `verbose`, `QUIT` included, though `-v` stops before it. `FtpSession.RunAsync` logs the outcome once: `error` `failed with <CurlExitCode> (<n>): <message>`, or `info` `transfer finished: <bytes> bytes in <ms> ms`; the handler logs a failed control connect the same way. Both `ConnectTarget`s carry the incoming log.
- Choice: `PASS` and `ACCT` are redacted by verb, ignoring case, wherever they come from (the login or a `-Q` quote), as `sent PASS (not logged)`. The session never sends `ACCT` itself yet (a `332` ends with exit 67), so `--ftp-account` is pinned by a test that its value appears in no message. No new ADR: ADR-0222 already fixes the levels, the component and the never-logged values.
- Tests: `FtpProtocolHandlerDiagnosticLogTests` (16) with a hand-rolled `RecordingDiagnosticLog` that also fails any write at a level it did not enable. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Ftp.UnitLibrary`: 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. FTP and FTPS sessions write login, TLS, directory, data connection, fallbacks, transfer and failures to the diagnostic log (component ftp), never a password
