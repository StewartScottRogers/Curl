---
id: BL-926
title: Log SMTP, IMAP and POP3 session steps to the diagnostic log
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-938]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-926 — Log SMTP, IMAP and POP3 session steps to the diagnostic log

## Goal

The SMTP, IMAP and POP3 handlers write the diagnostic log (components `smtp`, `imap`, `pop3`) from `ITransferContext.DiagnosticLog` for each session step: greeting, capabilities, STARTTLS/STLS, SASL login, the command that carries the transfer, and logout.

## Context

- The rules are BL-937's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-938's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `Curl.Protocol.Smtp.UnitLibrary`: `SmtpSession.cs`, `SmtpControlChannel.cs`, `SmtpSaslAuthentication.cs`, `SmtpMailTransaction.cs`, `SmtpCommandTransfer.cs`; `Curl.Protocol.Imap.UnitLibrary`: `ImapSession.cs`, `ImapControlChannel.cs`, `ImapAuthentication.cs`, `ImapAppend.cs`; `Curl.Protocol.Pop3.UnitLibrary`: `Pop3Session.cs`, `Pop3ControlChannel.cs`, `Pop3Login.cs`. Several of these wrap `ITransferContext`: each wrapper forwards `DiagnosticLog` from the context it wraps, and every `ConnectTarget` they build carries it.
- What, per level: `error` the reply that ends the session with its `CurlExitCode`; `warning` a SASL mechanism refused before another succeeded, a server with no usable mechanism; `info` greeting received, TLS upgraded, logged in (mechanism named), message sent or fetched with bytes and ms; `verbose` each command by verb (with arguments, except `PASS`, `AUTH` continuation lines and `LOGIN`'s password, which are never logged) and each reply's status.
- Credential-bearing paths: `-u user:s3cret` over SMTP `AUTH PLAIN`, IMAP `LOGIN` and POP3 `USER`/`PASS`.

## Acceptance criteria

- [x] Each of `Curl.Protocol.Smtp.UnitTests`, `Curl.Protocol.Imap.UnitTests` and `Curl.Protocol.Pop3.UnitTests` pins: the login at `info` with the mechanism; a STARTTLS (or STLS) upgrade at `info`; a refused command at `error` naming its `CurlExitCode`; a wrapper context returning the wrapped `DiagnosticLog`; and the no-secret test above.
- [x] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [x] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

- Shape: one internal static `<Protocol>DiagnosticLogLines` class per library (`SmtpDiagnosticLogLines`, `ImapDiagnosticLogLines`, `Pop3DiagnosticLogLines`), one small method per line, each testing `IsEnabled` before building its message (ADR-0222 decision 8). The libraries cannot share it: protocols never reference each other.
- Verbose: each control channel logs every line it sends (`sent <command>`) and each complete reply by status only (`reply 250`, `reply +OK`/`-ERR`/`+`, `reply A002 Ok`, `reply + Continuation`), never the reply text, which may carry a SASL challenge. Senders of a credential-bearing line pass what may be logged instead: `PASS <password not logged>`, `APOP u <digest not logged>`, `LOGIN u <password not logged>`, `AUTH`/`AUTHENTICATE <mech> <SASL response not logged>`, and `<SASL response not logged>` for every continuation answer. SASL challenges are never logged.
- Info: `greeting ... received`, `STARTTLS`/`STLS upgraded the connection to TLS`, `logged in with SASL <mech>` / `LOGIN` / `APOP` / `USER and PASS`, and at the handler `transfer done, N bytes in M ms` from `ITransferContext.TimeProvider`. Error: the handler logs `transfer failed with CurlExitCode.<Name> (<n>): <message>` for every failing result, connect failures included, so every exit path is covered at one point.
- Warning: SMTP a SASL mechanism cancelled before the next is chosen, and no usable mechanism; IMAP and POP3 no usable mechanism among those offered (they fall back to `LOGIN` / `USER`-`PASS` or fail).
- "Wrapper context": none of these three libraries wraps `ITransferContext`; the only object they build around it is the `ConnectTarget`, which now carries `DiagnosticLog`, and each project pins that with `ExecuteAsync_ConnectTarget_CarriesTheContextsDiagnosticLog`.
- The IMAP and POP3 channels take the log as an optional last constructor argument (null means `NoDiagnosticLog`), so their existing direct-construction tests stay unmodified. No new ADR: every choice sits inside ADR-0222's decisions 2, 6, 7 and 8. No option changed, so `--ai-help` is unaffected.
- Results: Smtp 242, Imap 352, Pop3 228 tests green; all three libraries 100% line and branch, 0 failing members; full solution build clean and fast tests green in all 33 test projects.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SMTP, IMAP and POP3 write their session steps to the diagnostic log, credentials redacted
