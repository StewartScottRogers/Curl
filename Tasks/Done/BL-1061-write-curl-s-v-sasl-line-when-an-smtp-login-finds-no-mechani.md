---
id: BL-1061
title: Write curl's -v SASL line when an SMTP login finds no mechanism
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-810]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1061 — Write curl's -v SASL line when an SMTP login finds no mechanism

## Goal

When an SMTP login ends exit 67 because no way of logging in is possible, `-v` writes the `* SASL: ...` line curl 8.21.0 writes before `* closing connection #0`, byte for byte.

## Context

- Found by BL-810, which did the same for POP3 (`Pop3Login.NoWayToLogIn`, `Pop3ProtocolHandlerNoLoginMechanismTests`): `no overlap between offered and configured auth mechanisms` once the server listed a SASL mechanism curl knows, `no auth mechanism was offered or recognized` when it listed none.
- Measured 2026-09-30 with `Record-CurlExchange.ps1 -Smtp`, `-sv --mail-from a@b --mail-rcpt c@d -T mail.txt smtp://127.0.0.1:<port>/`:
  - `-u user:secret`, `-SmtpReply 'EHLO=250-localhost\r\n250 AUTH FOO'`: `* SASL: no auth mechanism was offered or recognized`, `* closing connection #0`, exit 67.
  - `--oauth2-bearer tok` against the default EHLO (`AUTH PLAIN LOGIN`): `* SASL: no overlap between offered and configured auth mechanisms`, `* closing connection #0`, exit 67.
- An EHLO with no `AUTH` line at all skips the login; measure that and `--login-options AUTH=<mech>` before pinning.

## Acceptance criteria

- [x] Each measured case is recorded in Notes with its stderr.
- [x] `Curl.Protocol.Smtp.UnitTests` pins the info line for each measured case, and that a successful login and a failed exchange write none.
- [x] `Curl.Console.UnitTests` pins the `-v` stderr of the first case end to end.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-10-01 with curl 8.21.0 (Schannel), `Record-CurlExchange.ps1 -Smtp`,
`-sv --mail-from a@b --mail-rcpt c@d -T mail.txt <extra> smtp://127.0.0.1:<port>/`. Each
stderr below is what follows the `EHLO` reply lines; every failing case ends exit 67 and
writes nothing else (no `* Login denied` line):

- `-u user:secret`, EHLO `AUTH FOO`: `* SASL: no auth mechanism was offered or recognized`, `* closing connection #0`.
- `-u user:secret --login-options AUTH=PLAIN`, EHLO `AUTH FOO`: same as above.
- `--oauth2-bearer tok`, default EHLO (`AUTH PLAIN LOGIN CRAM-MD5`): `* SASL: no overlap between offered and configured auth mechanisms`, `* closing connection #0`.
- `-u --login-options AUTH=PLAIN`, EHLO `AUTH LOGIN`: `no overlap ...`.
- `--oauth2-bearer tok`, EHLO `AUTH SCRAM-SHA-256`: `no overlap ...`.
- `-u`, EHLO `AUTH SCRAM-SHA-256 SCRAM-SHA-1`: `* SASL: no auth mechanism offered could be selected`, `* SASL: SCRAM-SHA-256 not builtin`, `* SASL: SCRAM-SHA-1 not builtin`, `* closing connection #0`.
- `-u --login-options AUTH=*`, EHLO `AUTH SCRAM-SHA-1 SCRAM-SHA-256`: same three SASL lines, SHA-256 first.
- `-u --login-options AUTH=SCRAM-SHA-256`, EHLO `AUTH SCRAM-SHA-256 PLAIN`: `could be selected`, `SCRAM-SHA-256 not builtin`.
- `-u`, EHLO `AUTH FOO SCRAM-SHA-1` and `AUTH scram-sha-1`: `could be selected`, `SCRAM-SHA-1 not builtin`.
- `-u`, EHLO `250 localhost` (no AUTH line) and `250 AUTH` (bare): no login, upload succeeds, exit 0.
- `-u --login-options AUTH=CRAM-MD5`, default EHLO: logs in with CRAM-MD5, exit 0.
- `-u`, `-SmtpReply 'AUTH=535 no'`: `< 535 no`, `* closing connection #0`, exit 67 - no SASL line and no `Login denied` line.

Done: `SmtpSaslAuthentication.ReportNoWayToLogIn` writes the lines with the same rule as
IMAP (BL-1060) and POP3 (BL-810); `SmtpProtocolHandler.ReportConnectionEnd` no longer writes
`Login denied` as an info line, since curl's SASL code never `failf`s it (the refused
exchange measurement above). Tests: `SmtpProtocolHandlerNoLoginMechanismTests` (12) and
`CurlCommandRunnerSmtpTransferEventTests.RunAsync_VerboseLoginWithNoMechanism_WritesTheSaslLineAndClosesWithExit67`.
Measure-CodeQuality: Curl.Protocol.Smtp.UnitLibrary 769/769 lines, 308/308 branches, 0 failing members.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. SMTP -v writes curl's SASL line before closing when no login mechanism is usable
