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
completed:
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

- [ ] Each measured case is recorded in Notes with its stderr.
- [ ] `Curl.Protocol.Smtp.UnitTests` pins the info line for each measured case, and that a successful login and a failed exchange write none.
- [ ] `Curl.Console.UnitTests` pins the `-v` stderr of the first case end to end.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
