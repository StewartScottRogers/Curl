---
id: BL-926
title: Log SMTP, IMAP and POP3 session steps to the diagnostic log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-916]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-926 — Log SMTP, IMAP and POP3 session steps to the diagnostic log

## Goal

The SMTP, IMAP and POP3 handlers write the diagnostic log (components `smtp`, `imap`, `pop3`) from `ITransferContext.DiagnosticLog` for each session step: greeting, capabilities, STARTTLS/STLS, SASL login, the command that carries the transfer, and logout.

## Context

- The rules are BL-915's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-916's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `Curl.Protocol.Smtp.UnitLibrary`: `SmtpSession.cs`, `SmtpControlChannel.cs`, `SmtpSaslAuthentication.cs`, `SmtpMailTransaction.cs`, `SmtpCommandTransfer.cs`; `Curl.Protocol.Imap.UnitLibrary`: `ImapSession.cs`, `ImapControlChannel.cs`, `ImapAuthentication.cs`, `ImapAppend.cs`; `Curl.Protocol.Pop3.UnitLibrary`: `Pop3Session.cs`, `Pop3ControlChannel.cs`, `Pop3Login.cs`. Several of these wrap `ITransferContext`: each wrapper forwards `DiagnosticLog` from the context it wraps, and every `ConnectTarget` they build carries it.
- What, per level: `error` the reply that ends the session with its `CurlExitCode`; `warning` a SASL mechanism refused before another succeeded, a server with no usable mechanism; `info` greeting received, TLS upgraded, logged in (mechanism named), message sent or fetched with bytes and ms; `verbose` each command by verb (with arguments, except `PASS`, `AUTH` continuation lines and `LOGIN`'s password, which are never logged) and each reply's status.
- Credential-bearing paths: `-u user:s3cret` over SMTP `AUTH PLAIN`, IMAP `LOGIN` and POP3 `USER`/`PASS`.

## Acceptance criteria

- [ ] Each of `Curl.Protocol.Smtp.UnitTests`, `Curl.Protocol.Imap.UnitTests` and `Curl.Protocol.Pop3.UnitTests` pins: the login at `info` with the mechanism; a STARTTLS (or STLS) upgrade at `info`; a refused command at `error` naming its `CurlExitCode`; a wrapper context returning the wrapped `DiagnosticLog`; and the no-secret test above.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

## Log

- 2026-09-29: Created.
