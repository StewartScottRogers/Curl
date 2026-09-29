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
completed:
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

- [ ] `Curl.Protocol.Ftp.UnitTests` pin: a passive download logs `info` for login, the data connection and the transfer end; an EPSV refusal then PASV logs `warning`; at `verbose` a `USER` command appears and no message contains the password; a `550` on `RETR` logs `error` naming its `CurlExitCode`; the data connection's `ConnectTarget` carries the incoming `DiagnosticLog`.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

- BL-930 and BL-931 (curl's own FTP `-v`/`--trace` lines) touch the same library; whichever lands second keeps the other's tests green.

## Log

- 2026-09-29: Created.
