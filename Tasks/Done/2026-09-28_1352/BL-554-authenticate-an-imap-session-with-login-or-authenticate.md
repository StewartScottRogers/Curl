---
id: BL-554
title: Authenticate an IMAP session with LOGIN or AUTHENTICATE
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-553, BL-536]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-554 — Authenticate an IMAP session with LOGIN or AUTHENTICATE

## Goal

The IMAP handler logs in as curl 8.21.0 does: `AUTHENTICATE <mech>` through the injected SASL authenticator when `AUTH=` capabilities are offered (with `SASL-IR` and `--sasl-ir`), else `LOGIN` with curl's quoting of user and password, honouring `--login-options`, and a `NO` mapped to exit 67 and curl's message.

## Context

- Conformance audit 2026-09-28, rows 23 and 34. SASL: BL-533's ADR, BL-534, BL-536.
- Measure with `Record-CurlExchange.ps1 -Imap`: `-u u:p` with `AUTH=PLAIN` offered, with none offered (`LOGIN`), a password containing `"` and `\` and a space, `--login-options AUTH=LOGIN`, `SASL-IR` advertised with `--sasl-ir`, and `LOGIN` answered `NO`.

## Acceptance criteria

- [x] Measured first as above; request lines, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Imap.UnitTests` pin client bytes (quoting byte for byte) and outcome for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Protocol.Imap.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

### Measured (2026-09-28, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Imap`, `-sS ... imap://127.0.0.1:<P>/INBOX;UID=1`)

Default capability list is `IMAP4rev1 STARTTLS AUTH=PLAIN AUTH=LOGIN`; a case that overrides
it names the list. Every success goes on to `SELECT`, `UID FETCH`, `LOGOUT`, exit 0, empty stderr.

| Case | Client lines after `A001 CAPABILITY` | Exit, stderr |
| --- | --- | --- |
| `-u u:p`, default list | `A002 AUTHENTICATE PLAIN`, (after `+ `) `AHUAcA==` | 0 |
| `-u u:p`, `IMAP4rev1` only | `A002 LOGIN u p` | 0 |
| `-u 'us er:a"b\c d'`, no AUTH | `A002 LOGIN "us er" "a\"b\\c d"` | 0 |
| `-u u:`, no AUTH | `A002 LOGIN u ` (trailing space) | 0 |
| `--login-options AUTH=LOGIN` (also `auth=LOGIN`, `AUTH=login`, `AUTH=LOGIN;`, URL `u:p;AUTH=LOGIN@`) | `A002 AUTHENTICATE LOGIN`, `dQ==`, `cA==` | 0 |
| `AUTH=LOGIN`, only `AUTH=PLAIN` offered | nothing | 67 `curl: (67) Login denied` |
| `AUTH=+LOGIN` / `AUTH=+login`, default list | `A002 LOGIN u p` | 0 |
| `AUTH=*`, no AUTH | `A002 LOGIN u p` | 0 |
| `AUTH=BOGUS`, `FOO=1`, `AUTH=` | nothing, not even `CAPABILITY` | 3 `curl: (3) URL using bad/illegal format or missing URL` |
| `AUTH=LOGIN;AUTH=PLAIN`, only PLAIN offered | `A002 AUTHENTICATE PLAIN`, `AHUAcA==` | 0 |
| `SASL-IR AUTH=PLAIN` (with or without `--sasl-ir`; also lower-case `sasl-ir`) | `A002 AUTHENTICATE PLAIN AHUAcA==` | 0 |
| `--sasl-ir`, `AUTH=PLAIN` without SASL-IR | `A002 AUTHENTICATE PLAIN AHUAcA==` | 0 |
| `--sasl-ir --login-options AUTH=LOGIN` | `A002 AUTHENTICATE LOGIN dQ==`, `cA==` | 0 |
| `LOGIN=NO denied`, `LOGIN=BAD what`, `LOGIN=NO` | `A002 LOGIN u p`, no LOGOUT | 67 `curl: (67) Access denied. \x02` |
| `LOGIN=PREAUTH x` | `A002 LOGIN u p` | 67 `curl: (67) Access denied. \x03` |
| `AUTHENTICATE=NO denied` | `A002 AUTHENTICATE PLAIN`, no LOGOUT | 67 `Login denied` |
| `LOGINDISABLED` (any case), no AUTH | nothing | 67 `Login denied` |
| `LOGINDISABLED AUTH=PLAIN` with `AUTH=+LOGIN` | nothing | 67 `Login denied` |
| `AUTH=FOO` only (unknown), `AUTH=plain` (lower-case) | `LOGIN u p`; `AUTHENTICATE PLAIN` | 0 |
| no `-u` | straight to `A002 SELECT INBOX` | 0 |
| `* PREAUTH` greeting with `-u` | straight to `A002 SELECT INBOX` | 0 |
| `--oauth2-bearer tok`, no `-u`, `AUTH=XOAUTH2`, answered `OK` at once | `A002 AUTHENTICATE XOAUTH2` | 67 `Login denied` |
| `--oauth2-bearer tok`, no `-u`, no AUTH | nothing | 67 `Login denied` |
| `CAPABILITY` answered `NO` but listing `AUTH=PLAIN` | `A002 AUTHENTICATE PLAIN`, `AHUAcA==` | 0 |

The `\x02`/`\x03` byte is real: curl prints its response code with `%c` (`IMAP_RESP_NOT_OK` 2,
`IMAP_RESP_PREAUTH` 3), confirmed with `od -c` on stderr.

### What was built

- `ImapLoginOptions` reads `--login-options` (else the URL's `;` options) as curl's
  `imap_parse_url_options` does: `AUTH=` keys and mechanisms in any case, `AUTH=+LOGIN`,
  `AUTH=*`, several `AUTH=` accumulating, exit 3 for anything else.
- `ImapAuthentication` runs `AUTHENTICATE` through the injected `ISaslAuthenticator`, or `LOGIN`
  quoted by `ImapQuoting`; `ImapSession` calls it after `CAPABILITY`/`STARTTLS` unless the
  greeting was `PREAUTH`, resetting the offered mechanisms at each `CAPABILITY` as curl does.
- `ImapControlChannel.ReadResponseAsync` accepts a `+` continuation when asked (new
  `ImapResponseStatus.Continuation`); `SendLineAsync` sends the untagged base64 answers.
- `ImapProtocolHandler` gains a constructor taking the `ISaslAuthenticator`; BL-558 registers it.

### Choices (sensible defaults, no ADR needed)

- `SaslRequest.RequiredMechanism` holds one mechanism, but curl's options may allow several.
  The handler narrows the offered list to the allowed ones and asks with no required
  mechanism, first asking with `EXTERNAL` required when the options name it (curl ranks
  EXTERNAL first and only uses it when named). This keeps the shared contract unchanged.
- A handler built without an authenticator logs in with `LOGIN` only, as the
  `ISaslAuthenticator` remarks already state.
- A continuation that is not base64 is handed to the exchange empty, as ADR-0133 settled for
  SMTP; an `OK` before any SASL message has been sent is exit 67 (measured with XOAUTH2).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. The IMAP handler logs in with AUTHENTICATE through the SASL authenticator (SASL-IR, --sasl-ir) or LOGIN with curl's quoting, honours --login-options, and maps refusals to exit 67 with curl's messages
