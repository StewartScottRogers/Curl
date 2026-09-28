---
id: BL-547
title: Open a POP3 session: greeting, CAPA, STLS, pop3s and QUIT
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-534, BL-530]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-547 — Open a POP3 session: greeting, CAPA, STLS, pop3s and QUIT

## Goal

A `Pop3ProtocolHandler` in `Curl.Protocol.Pop3.UnitLibrary` connects through `IConnector`, reads the greeting (keeping the APOP timestamp), sends `CAPA`, upgrades with `STLS` per `--ssl`/`--ssl-reqd` or starts in TLS for `pop3s://`, ends with `QUIT`, and maps every failure to curl 8.21.0's exit code and message.

## Context

- Conformance audit 2026-09-28, row 34 (Blocker, L; POP3 split: this session, BL-548 auth, BL-549 list and retrieve, BL-550 custom commands and options, BL-551 registration, BL-552 `-v`).
- Design: BL-533's ADR; contract: BL-534; timeouts: BL-498's ADR; endpoints: BL-515's ADR. `Curl.Protocol.Pop3.UnitLibrary/CLAUDE.md` rules apply (Abstractions only, `IConnection`, no `Socket`/`SslStream`).
- Measure with `Record-CurlExchange.ps1 -Pop3` (BL-530): the default session, a `-ERR` greeting, `CAPA` answered `-ERR`, `STLS` refused under `--ssl` and `--ssl-reqd`, `pop3s://` with `-Tls -k`, a malformed reply, and the server closing early.

## Acceptance criteria

- [x] Measured first as above; request lines, stdout, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Pop3.UnitTests` drive the handler through a fake `IConnector`/`IConnection` replaying the measured bytes and pin client bytes and outcome for each case, including multi-line `CAPA` split across reads.
- [x] No test needs `TestCategory=Integration`; tests are platform-neutral.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (curl 8.21.0 Schannel, 2026-09-28, `Record-CurlExchange.ps1 -Pop3`)

curl ran `-sS pop3://127.0.0.1:18110/` unless stated. Without `-u` curl authenticates with
nothing. stdout was the `LIST` output (14 bytes) on every exit 0 and empty otherwise.
Server replies are the recorder's defaults unless overridden.

| Case | Client lines | Exit | stderr |
| --- | --- | --- | --- |
| default | `CAPA`, `LIST`, `QUIT` | 0 | - |
| `GREETING=-ERR go away`, `-ERRx`, `+junk`, `+ok hi` | nothing | 8 | `curl: (8) Got unexpected pop3-server response` |
| `GREETING=junk\r\n+OK hi`, `GREETING=+OKjunk` | as default | 0 | - |
| `GREETING=-ER`, `GREETING=hello there` (skipped; server hangs up after 2 s) | nothing | 56 | `curl: (56) response reading failed (errno: 0)` |
| `CAPA=CLOSE` | `CAPA` | 56 | `curl: (56) response reading failed (errno: 0)` |
| `CAPA=-ERR no`, also with `--ssl` | `CAPA`, `LIST`, `QUIT` | 0 | - |
| `--ssl-reqd -k`, `CAPA=-ERR no` | `CAPA` | 64 | `curl: (64) STLS not supported.` |
| `--ssl-reqd`, CAPA `+OK`/`USER`/`.` (no STLS), or `XSTLS` | `CAPA` | 64 | `curl: (64) STLS not supported.` |
| `--ssl-reqd -k`, CAPA `+OK`/`STLS`/`-ERR x`/`.` | `CAPA` | 64 | `curl: (64) STLS not supported.` |
| `--ssl`, `STLS=-ERR not now` | `CAPA`, `STLS`, `LIST`, `QUIT` | 0 | - |
| `--ssl-reqd`, `STLS=-ERR not now` or `STLS=+` | `CAPA`, `STLS` | 64 | `curl: (64) STARTTLS denied` |
| `--ssl-reqd -k` | `CAPA`, `STLS`, TLS, `CAPA`, `LIST`, `QUIT` | 0 | - |
| `--ssl-reqd` without `-k` | `CAPA`, `STLS`, handshake fails | 60 | `curl: (60) schannel: SEC_E_UNTRUSTED_ROOT ...` (the TLS provider's) |
| CAPA listing `stls`, `STLSX`, `.x` and `. ` before `STLS`, or no `+OK` status line | `STLS` sent in each | - | - |
| `pop3s://` with `-Tls -k`, also `--ssl-reqd` | `CAPA`, `LIST`, `QUIT`, no `STLS` | 0 | - |
| greeting line of 65533 chars + CRLF | as default | 0 | - |
| greeting line of 65534 chars + CRLF | nothing | 100 | `curl: (100) A value or data field grew larger than allowed` |
| `QUIT=-ERR no` | as default | 0 | - |
| lines ending LF only | parsed as with CRLF | - | - |

### Plan and decisions

- Mirrors BL-540's SMTP shape: `Pop3ProtocolHandler(IConnector, ITlsProvider)` connects
  (TLS for `pop3s`, ports 110/995 from `CurlUrl.Port`), `Pop3Session` runs greeting, CAPA,
  STLS and QUIT, `Pop3ControlChannel` reads lines (ADR-0121 §7). Every rule is the measured
  behaviour above, so no ADR was needed.
- Line rules: a status line starts `+` or `-ERR`, anything else is skipped; only `+OK`
  (capitals) is success. During CAPA every line counts, only a line that is exactly `.`
  ends it, and a `-ERR`-prefixed line refuses it (curl leaves the rest unread). `STLS` is
  matched as a case-insensitive prefix.
- The greeting's APOP timestamp is kept on `Pop3Session.ApopTimestamp` for BL-548. Its
  rule (ends with `>`, from the first `<`, must contain `@`) follows curl's
  `pop3_state_servergreet_resp`; APOP itself is not observable until BL-548, which
  measures it. The CAPA lines are kept on `Pop3Session.Capabilities` for the SASL/USER
  choice.
- Interim: once open, the session sends `QUIT` and reports success; `LIST`/`RETR` are
  BL-549's. The handler is not registered in `Curl.Console` (BL-551), so no user reaches it.
- Pipeline stages ran in-session (plan, tests, implementation, verify, quality gate); no
  separate reviewer or conformance agent. Every pinned byte comes from the table above.

### Results

- 59 tests in `Curl.Protocol.Pop3.UnitTests`, all fake connections, no Integration category.
- `Measure-CodeQuality.ps1 -Library Curl.Protocol.Pop3.UnitLibrary`: 100% line, 100%
  branch, 28 members, 0 failing, worst CRAP 10.
- `dotnet build Curl.slnx -warnaserror` clean; every fast test project passed.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. Pop3ProtocolHandler opens pop3/pop3s sessions: greeting with APOP timestamp, CAPA, STLS per --ssl/--ssl-reqd, QUIT, with curl 8.21.0's exit codes and messages
