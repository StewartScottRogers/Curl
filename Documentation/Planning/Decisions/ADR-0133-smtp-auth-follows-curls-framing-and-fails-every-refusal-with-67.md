# ADR-0133 — SMTP AUTH follows curl's framing and fails every refusal with 67

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-541.

## Context

BL-541 makes the SMTP handler authenticate with `AUTH` through ADR-0121's injected
`ISaslAuthenticator`. The authenticator chooses the mechanism and builds its messages; the
handler owns the framing: which `EHLO` lines offer mechanisms, when to authenticate at all,
where the initial response goes, how `334` is answered and what ends the exchange.

Measured on Windows against curl 8.21.0 (mingw, Schannel) with
`Record-CurlExchange.ps1 -Smtp`, 2026-09-28, `-u u:p` unless stated:

| Case | curl sent / did |
| --- | --- |
| `250-AUTH PLAIN LOGIN` | `AUTH PLAIN`, `AHUAcA==` after `334 `, then `MAIL FROM` after `235` |
| same, `--sasl-ir` | `AUTH PLAIN AHUAcA==` |
| `--login-options AUTH=LOGIN` | `AUTH LOGIN`, `dQ==` after `334 VXNlcm5hbWU6`, `cA==` after `334 UGFzc3dvcmQ6` |
| same, `--sasl-ir` | `AUTH LOGIN dQ==`, `cA==` after `334 UGFzc3dvcmQ6` |
| `AUTH` answered `535`, `504`, or `250` | exit 67 `Login denied`, nothing more sent, no `QUIT` |
| `--sasl-ir`, `AUTH PLAIN ...` answered `334 ` | exit 67, no reply to the `334` |
| `AUTH OAUTHBEARER` (no `--sasl-ir`) answered `235` at once | exit 67 |
| no `AUTH` line; `AUTH=PLAIN`; `AUTH<tab>PLAIN` | no `AUTH` sent, mail delivered |
| `250-auth PLAIN` (lower case) | `AUTH PLAIN` |
| `250 AUTH PLAIN` / `250 AUTH LOGIN` as the final line | `AUTH PLAIN` / `AUTH LOGIN` |
| `250-AUTH LOGIN` and `250-AUTH PLAIN` | `AUTH PLAIN` (both lines count) |
| `250-AUTH PLAINX LOGIN` | `AUTH LOGIN` (a word must match a mechanism exactly) |
| `250-AUTH ` (no mechanisms) | exit 67 `Login denied`, no `AUTH` sent |
| no `-u` | no `AUTH`, mail delivered |
| `--oauth2-bearer tok --sasl-ir`, no `-u` | `AUTH OAUTHBEARER` with `n,a=,...` |
| `--login-options AUTH=EXTERNAL --sasl-ir`, no `-u` | `AUTH EXTERNAL =` |
| `-u : --sasl-ir --login-options AUTH=LOGIN` | `AUTH LOGIN =`, then `=` |
| `smtp://u:p;AUTH=LOGIN@host/x` | `AUTH LOGIN` |
| `EHLO` refused, `HELO` accepted | no `AUTH`, mail delivered |
| server hangs up after `AUTH PLAIN` | exit 56 `response reading failed (errno: 0)` |
| `--sasl-ir`, user of 368 `u`s (base64 496) | initial response on the `AUTH` line |
| `--sasl-ir`, user of 371 `u`s (base64 500) | `AUTH PLAIN`, the response after `334 ` |

ADR-0123 recorded that curl ignores the last mechanism of a final `250 AUTH ...` line.
Re-measured here with `250 AUTH PLAIN` and `250 AUTH LOGIN PLAIN`, curl used every
mechanism on the line; that observation does not reproduce and is not implemented.

## Decision

1. **Offered mechanisms.** Every `EHLO` reply line whose text after the code starts with
   `AUTH ` (any case, a space and not `=` or a tab after it) offers the words after it,
   split on spaces and tabs; all such lines are joined. No such line: no authentication. A
   `HELO` session never authenticates, and after `STARTTLS` only the second `EHLO` counts.
2. **Whether to authenticate.** Only with `-u` (any credential, even `:`), a bearer token,
   or `AUTH=EXTERNAL` with `EXTERNAL` offered. Otherwise the mail goes out unauthenticated.
3. **No usable mechanism** (`ChooseMechanism` answers `null`, a bare `AUTH ` line
   included): exit 67 `Login denied` with nothing sent.
4. **Login options.** `--login-options` when given, else the URL's `;` options; the first
   `AUTH=` value (key in any case) is `SaslRequest.RequiredMechanism`. The service name is
   `--service-name` or `smtp`; the host is the URL's host.
5. **Initial response.** Under `--sasl-ir` it goes on the `AUTH` line while the mechanism's
   name plus the base64 is at most 504 characters (curl's 512-byte line less `AUTH `, the
   space and CRLF); otherwise, or without `--sasl-ir`, it answers the first `334`. An
   empty message is sent as `=`.
6. **The exchange.** Each `334` is answered - with the pending initial response first,
   then with `ISaslExchange.Respond` on the decoded challenge. `235` after at least one
   message is success. Anything else - another code, `235` before any message, or
   `Respond` answering `null` - is exit 67 `Login denied` with nothing more sent and no
   `QUIT`. This departs from `ISaslExchange.Respond`'s documentation, which says the
   handler cancels with `*`: curl sends nothing when a built mechanism gets a challenge it
   does not expect (measured above and in ADR-0123).
7. **An undecodable challenge** (not base64, or a bare `334`) is handed to the exchange
   as empty. No mechanism built so far reads its challenge, and curl ignores it for them.
   CRAM-MD5, DIGEST-MD5, NTLM and GSSAPI (BL-537, BL-538) read it; curl cancels those with
   `*` and tries the next mechanism, which is theirs to measure and add.

## Consequences

- `SmtpProtocolHandler` gains a public constructor taking an `ISaslAuthenticator`; the
  two-argument one never sends `AUTH`. `Curl.Console` does not register the SMTP handler
  yet, so wiring the authenticator in is for the task that registers it.
- The framing is generic: a mechanism added to `Curl.Authentication.UnitLibrary` needs no
  SMTP change.
- Known gap: curl's per-mechanism state machine fails a `235` that arrives between
  LOGIN's user name and password; the generic rule treats it as success. No server is
  known to do this.
- A second `AUTH=` in the login options is ignored; curl ORs them into a set, which
  `SaslRequest` cannot carry.

## Alternatives considered

- **Cancel with `*` when `Respond` answers `null`, as the contract says.** Sends a line
  curl does not send.
- **Fail an undecodable challenge with 67.** Would refuse a server curl talks to happily
  for PLAIN and LOGIN.
- **Track a per-mechanism state machine in the handler.** Would duplicate the
  authenticator's knowledge in every mail handler, against ADR-0121.

## Amendment - BL-774, 2026-09-29

Decided by Claude under Stewart's delegation. Measured against curl 8.21.0 (mingw,
Schannel) with `Record-CurlExchange.ps1 -Smtp`: point 7 now holds only for mechanisms that
ignore their challenge (PLAIN, LOGIN, EXTERNAL, XOAUTH2, OAUTHBEARER). A `334` whose text
is not base64 (empty text or text starting `=` is an empty challenge, not a bad one), for a
mechanism that reads it - every GSSAPI challenge, the first CRAM-MD5, DIGEST-MD5 and NTLM
answer - is cancelled with `*`. The server's reply to `*` is read whatever it is (501, 334
and 235 alike), the mechanism is dropped from the offered set and the authenticator chooses
again; with none left the transfer fails with exit 67 `Authentication cancelled` and no
`QUIT`. A challenge the exchange cannot answer (`Respond` null) is still 67 `Login denied`.
