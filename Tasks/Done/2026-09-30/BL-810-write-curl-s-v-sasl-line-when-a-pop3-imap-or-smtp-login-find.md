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
completed: 2026-09-30
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

- [x] Each case above, and each further case measured, is recorded in Notes with its stderr.
- [x] `Curl.Protocol.Pop3.UnitTests` pins the info line for each measured case, and that a successful fallback and a failed exchange write none.
- [x] `Curl.Console.UnitTests` pins the `-v` stderr of the first case end to end.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-30, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Pop3`,
`-sv <args> pop3://127.0.0.1:<port>/1`. Each failing case sent only `CAPA` and ended exit 67
with the lines shown, then `* closing connection #0`:

| Case (`-Pop3Reply` / args) | stderr line(s) before `closing connection` |
| --- | --- |
| CAPA `TOP` only, `-u user:secret` | `* SASL: no auth mechanism was offered or recognized` |
| CAPA `SASL FOO`, `-u` | `* SASL: no auth mechanism was offered or recognized` |
| default CAPA (`SASL PLAIN LOGIN`, `USER`), `--oauth2-bearer tok` | `* SASL: no overlap between offered and configured auth mechanisms` |
| default CAPA, `-u`, `--login-options AUTH=CRAM-MD5` | `* SASL: no overlap ...` |
| CAPA `TOP`, `AUTH=*` | `* SASL: no auth mechanism was offered or recognized` |
| CAPA `SASL FOO`, `AUTH=*` | `* SASL: no auth mechanism was offered or recognized` |
| CAPA `USER` only, `--oauth2-bearer tok` | `* SASL: no auth mechanism was offered or recognized` |
| CAPA `SASL FOO`, `--oauth2-bearer tok` | `* SASL: no auth mechanism was offered or recognized` |
| CAPA `SASL XOAUTH2`, `-u`, `AUTH=PLAIN` | `* SASL: no overlap ...` |
| CAPA `SASL EXTERNAL`, `-u :secret` | `* SASL: no overlap ...` |
| CAPA refused (`-ERR no`), `-u`, `AUTH=PLAIN` | `* SASL: no auth mechanism was offered or recognized` |
| CAPA `USER` only, `-u`, `AUTH=PLAIN` | `* SASL: no auth mechanism was offered or recognized` |
| default CAPA, no timestamp, `-u`, `AUTH=+APOP` | `* SASL: no overlap ...` |
| CAPA `SASL plain`, `-u`, `AUTH=LOGIN` | `* SASL: no overlap ...` (mechanism names known in any case) |
| CAPA `SASL SCRAM-SHA-1 SCRAM-SHA-256 FOO`, `-u` | `* SASL: no auth mechanism offered could be selected`, `* SASL: SCRAM-SHA-256 not builtin`, `* SASL: SCRAM-SHA-1 not builtin` |
| CAPA `SASL SCRAM-SHA-256 PLAIN`, `-u`, `AUTH=SCRAM-SHA-256` | `* SASL: no auth mechanism offered could be selected`, `* SASL: SCRAM-SHA-256 not builtin` |
| CAPA `SASL scram-sha-1`, `-u` | `* SASL: no auth mechanism offered could be selected`, `* SASL: SCRAM-SHA-1 not builtin` |
| CAPA `SASL SCRAM-SHA-256`, `--oauth2-bearer tok` or `-u` + `AUTH=PLAIN` | `* SASL: no overlap ...` |

Writing none: `SASL FOO` + `USER` (USER/PASS, exit 0), `SASL FOO` + APOP timestamp (APOP,
exit 0), `SASL SCRAM-SHA-256` + `USER` (USER/PASS, exit 0), and `AUTH=-ERR denied` (exit 67,
`* closing connection #0` only).

Rule implemented (`Pop3Login.NoWayToLogIn`): when no login is possible, SCRAM mechanisms
offered (any case) that credentials (`-u`) and the login options (any way, or `AUTH=` naming
it) allow give "could be selected" plus one "not builtin" line each, SHA-256 first; else any
offered mechanism curl knows gives "no overlap"; else "offered or recognized".

Decision (Claude): "not builtin" is SCRAM-SHA-1/256 only, as measured on the Schannel build,
which builds them in only with libgsasl; recorded here rather than in an ADR because it pins
measured output, not a design choice. If the Linux/macOS OpenSSL builds differ, a task per
platform should pin theirs.

IMAP and SMTP write the same lines (measured: IMAP `LOGINDISABLED AUTH=FOO` -> "offered or
recognized", bearer -> "no overlap"; SMTP `AUTH FOO` -> "offered or recognized", bearer ->
"no overlap"): filed as BL-1060 (IMAP) and BL-1061 (SMTP).

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. -v writes curl's SASL line (offered or recognized / no overlap / SCRAM not builtin) before closing when a POP3 login has no way in
