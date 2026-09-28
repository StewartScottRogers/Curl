---
id: BL-553
title: Open an IMAP session: greeting, CAPABILITY, STARTTLS, imaps, tags and LOGOUT
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-534, BL-531]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-553 — Open an IMAP session: greeting, CAPABILITY, STARTTLS, imaps, tags and LOGOUT

## Goal

An `ImapProtocolHandler` in `Curl.Protocol.Imap.UnitLibrary` connects through `IConnector`, reads the greeting and untagged responses, issues tagged commands with curl 8.21.0's tag sequence, sends `CAPABILITY`, upgrades with `STARTTLS` per `--ssl`/`--ssl-reqd` or starts in TLS for `imaps://`, ends with `LOGOUT`, reads literals (`{n}`), and maps every failure to curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 34 (Blocker, L; IMAP split: this session, BL-554 auth, BL-555 fetch, BL-556 list/search/custom, BL-557 append, BL-558 registration, BL-559 `-v`).
- Design: BL-533's ADR; contract: BL-534; timeouts: BL-498's ADR; endpoints: BL-515's ADR. `Curl.Protocol.Imap.UnitLibrary/CLAUDE.md` rules apply.
- Measure with `Record-CurlExchange.ps1 -Imap` (BL-531): the default session (record the tag format curl uses), a `* BYE` greeting, a `* PREAUTH` greeting, `STARTTLS` refused under `--ssl` and `--ssl-reqd`, `imaps://` with `-Tls -k`, a tagged `BAD`, and the server closing early.

## Acceptance criteria

- [x] Measured first as above; request lines, stdout, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Imap.UnitTests` drive the handler through a fake connection replaying the measured bytes and pin client bytes (tags included) and outcome for each case, including a literal split across reads.
- [x] No test needs `TestCategory=Integration`; tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

Measured 2026-09-28 against curl 8.21.0 (Schannel) with
`Record-CurlExchange.ps1 -Port 18143 -Imap`, curl `-sS imap://127.0.0.1:18143/` unless
noted. `>` is curl, `<` the recorder. Where stdout is 69 bytes it is the `LIST` output,
which is BL-556's; this task's handler sends `LOGOUT` in `LIST`'s place, so `LOGOUT`
takes the tag `LIST` had.

- Default, exit 0, stderr empty: `< * OK [CAPABILITY ...] ready`, `> A001 CAPABILITY`,
  `< * CAPABILITY IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN`, `< A001 OK`,
  `> A002 LIST "" *`, `> A003 LOGOUT`, `< * BYE`, `< A003 OK`. Tags are `A` and three
  digits from `A001`.
- `GREETING=* BYE go away`: exit 8, `curl: (8) Got unexpected imap-server response`,
  nothing sent.
- `GREETING=* PREAUTH welcome`: exit 0, same exchange as the default, no `STARTTLS`.
- `* PREAUTH` with `--ssl-reqd`: `A001 CAPABILITY` only, exit 64,
  `curl: (64) STARTTLS not available.`
- `STARTTLS=NO refused` with `--ssl -k`: `A002 STARTTLS`, `< A002 NO refused`, then
  plaintext `A003 LIST`, `A004 LOGOUT`, exit 0.
- `STARTTLS=NO refused` with `--ssl-reqd -k`: stops after `A002 NO`, exit 64,
  `curl: (64) STARTTLS denied`, no `LOGOUT`.
- `STARTTLS` accepted, `--ssl-reqd -k`: handshake, `A003 CAPABILITY` again over TLS,
  `A004 LIST`, `A005 LOGOUT`, exit 0.
- `imaps://` with `-Tls -k`: TLS from the first byte, `A001 CAPABILITY`, `A002 LIST`,
  `A003 LOGOUT`, no `STARTTLS`, exit 0.
- `CAPABILITY=BAD no` with `--ssl-reqd`: exit 64, `curl: (64) STARTTLS not available.`,
  no `LOGOUT`.
- `CAPABILITY=CLOSE` (server closes early): exit 56,
  `curl: (56) response reading failed (errno: 0)`.

The implementation (from the run cut off earlier) matched every recording. This run split
`ImapSession.IsCapabilityResponse` (complexity 18) and `CapabilityAsync` (16) and moved
the session's `await using` into `ImapProtocolHandler.RunSessionAsync`, as the SMTP
handler does, so the cancellation path is covered. It also added tests for cancellation
and a bare `* ` line. Result: 83 tests, 100% line and branch coverage, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. imap:// and imaps:// open a session (greeting, CAPABILITY, STARTTLS per --ssl/--ssl-reqd, A001 tags, literals) and close it with LOGOUT, with curl's exit codes
