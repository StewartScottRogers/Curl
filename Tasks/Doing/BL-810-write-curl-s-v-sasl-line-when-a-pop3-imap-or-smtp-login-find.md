---
id: BL-810
title: Write curl's -v SASL line when a POP3, IMAP or SMTP login finds no mechanism
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-552]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-810 — Write curl's -v SASL line when a POP3, IMAP or SMTP login finds no mechanism

## Goal

When a POP3 login ends exit 67 because no way of logging in is possible, `-v` writes the `* SASL: ...` line curl 8.21.0 writes before `* closing connection #0`, byte for byte.

## Context

- Found by BL-552 (its Notes hold the recordings). Measured with `Record-CurlExchange.ps1 -Pop3`, `-sv -u user:secret pop3://127.0.0.1:<port>/1`:
  - `-Pop3Reply 'GREETING=+OK POP3 ready','CAPA=+OK\r\nTOP\r\n.'` (no SASL, no USER, no APOP timestamp): `* SASL: no auth mechanism was offered or recognized`, then `* closing connection #0`, exit 67.
  - The same with `CAPA=+OK\r\nSASL FOO\r\n.`: the same line.
  - `--oauth2-bearer tok` alone against the default CAPA (`SASL PLAIN LOGIN`): `* SASL: no overlap between offered and configured auth mechanisms`, then `* closing connection #0`.
  - `SASL FOO` with `USER` offered, or with an APOP timestamp, logs in with `USER`/`PASS` or `APOP` and writes neither line; a failed SASL exchange (`AUTH=-ERR denied`) writes neither.
- Today `Pop3Login.LoginDenied()` returns exit 67 `Login denied` (not written by `-v`, `Pop3SessionMessages.IsWrittenByVerbose`) with no info line.
- Measure which line each other no-mechanism case gets (`--login-options AUTH=CRAM-MD5` against `SASL PLAIN LOGIN`, `AUTH=*` with nothing offered) before pinning. SMTP and IMAP likely write the same lines through curl's shared SASL code: measure them, and file a task for each that does rather than widening this one.

## Acceptance criteria

- [ ] Each case above, and each further case measured, is recorded in Notes with its stderr.
- [ ] `Curl.Protocol.Pop3.UnitTests` pins the info line for each measured case, and that a successful fallback and a failed exchange write none.
- [ ] `Curl.Console.UnitTests` pins the `-v` stderr of the first case end to end.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
