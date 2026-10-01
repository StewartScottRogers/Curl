---
id: BL-1060
title: Write curl's -v SASL line when an IMAP login finds no mechanism
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-810]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-30
completed: 2026-10-01
---
# BL-1060 — Write curl's -v SASL line when an IMAP login finds no mechanism

## Goal

When an IMAP login ends exit 67 because no way of logging in is possible, `-v` writes the `* SASL: ...` line curl 8.21.0 writes before `* closing connection #0`, byte for byte.

## Context

- Found by BL-810, which did the same for POP3 (`Pop3Login.NoWayToLogIn`, `Pop3ProtocolHandlerNoLoginMechanismTests`): `no overlap between offered and configured auth mechanisms` once the server listed a SASL mechanism curl knows, `no auth mechanism was offered or recognized` when it listed none.
- Measured 2026-09-30 with `Record-CurlExchange.ps1 -Imap`, `-sv ... imap://127.0.0.1:<port>/INBOX`:
  - `-u user:secret`, `-ImapReply 'CAPABILITY=* CAPABILITY IMAP4rev1 LOGINDISABLED AUTH=FOO\r\nOK done','GREETING=* OK ready'`: `* SASL: no auth mechanism was offered or recognized`, `* closing connection #0`, exit 67.
  - `--oauth2-bearer tok` against the default capabilities (`AUTH=PLAIN AUTH=LOGIN`): `* SASL: no overlap between offered and configured auth mechanisms`, `* closing connection #0`, exit 67.
- Measure `--login-options AUTH=<mech>` against what is offered before pinning it.

## Acceptance criteria

- [x] Each measured case is recorded in Notes with its stderr.
- [x] `Curl.Protocol.Imap.UnitTests` pins the info line for each measured case, and that a `LOGIN` fallback and a failed exchange write none.
- [x] `Curl.Console.UnitTests` pins the `-v` stderr of the first case end to end.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured 2026-10-01, curl 8.21.0 (mingw, Schannel), `Record-CurlExchange.ps1 -Imap`,
  `-sv <args> imap://127.0.0.1:<port>/INBOX`. Every exit-67 case sent only `A001 CAPABILITY`;
  its stderr after `< A001 OK ...` is the lines shown, then `* closing connection #0`.
  - `-u user:secret`, CAPABILITY `IMAP4rev1 LOGINDISABLED AUTH=FOO`: `* SASL: no auth mechanism was offered or recognized`, exit 67.
  - `--oauth2-bearer tok`, default `IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN`: `* SASL: no overlap between offered and configured auth mechanisms`, exit 67.
  - `-u user:secret --login-options AUTH=NTLM`, default capabilities: `no overlap`, exit 67.
  - `-u user:secret --login-options AUTH=+LOGIN`, `IMAP4rev1 LOGINDISABLED AUTH=PLAIN`: `no overlap`, exit 67.
  - `-u user:secret`, `IMAP4rev1 LOGINDISABLED`: `no auth mechanism was offered or recognized`, exit 67.
  - `-u user:secret --login-options AUTH=NTLM`, `IMAP4rev1 AUTH=FOO`: `no auth mechanism was offered or recognized`, exit 67.
  - `--oauth2-bearer tok`, `IMAP4rev1 LOGINDISABLED`: `no auth mechanism was offered or recognized`, exit 67.
  - `-u user:secret --login-options AUTH=PLAIN`, `IMAP4rev1 AUTH=LOGIN`: `no overlap`, exit 67.
  - `-u user:secret`, `IMAP4rev1 LOGINDISABLED AUTH=SCRAM-SHA-256`: `* SASL: no auth mechanism offered could be selected`, `* SASL: SCRAM-SHA-256 not builtin`, exit 67.
  - `-u user:secret`, `IMAP4rev1 LOGINDISABLED AUTH=scram-sha-256 AUTH=SCRAM-SHA-1`: `could be selected`, `SCRAM-SHA-256 not builtin`, `SCRAM-SHA-1 not builtin` (curl's fixed order, not the offer's), exit 67.
  - `-u user:secret --login-options AUTH=SCRAM-SHA-1`, `AUTH=SCRAM-SHA-256 AUTH=SCRAM-SHA-1`: `could be selected`, `SCRAM-SHA-1 not builtin`, exit 67.
  - `--oauth2-bearer tok` or `--login-options AUTH=+LOGIN`, `LOGINDISABLED AUTH=SCRAM-SHA-256`: `no overlap`, exit 67.
  - `-u user:secret`, `IMAP4rev1 AUTH=FOO` or `AUTH=SCRAM-SHA-256`: `LOGIN user secret`, exit 0, no `SASL:` line.
  - `-u user:secret`, default capabilities, `AUTHENTICATE=NO denied`: `AUTHENTICATE PLAIN`, `* closing connection #0`, exit 67, no `SASL:` line.
- The rule is POP3's from BL-810, so `ImapAuthentication.NoWayToLogIn` follows `Pop3Login.NoWayToLogIn`;
  the lines live in `ImapInfoLines`, and `ImapLoginOptions.IsKnownMechanism` exposes the known-mechanism set.
  No new design decision, so no ADR.

## Log

- 2026-09-30: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. -v writes curl's SASL no-mechanism lines before closing an IMAP login that ends exit 67
