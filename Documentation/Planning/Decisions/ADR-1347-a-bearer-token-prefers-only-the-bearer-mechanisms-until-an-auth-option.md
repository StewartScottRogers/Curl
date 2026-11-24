# ADR-1347: --oauth2-bearer prefers only the bearer mechanisms until an AUTH= login option

- Status: Accepted
- Date: 2026-11-24
- Decided by Claude under Stewart's delegation
- Task: BL-5880 (fitness finding STD-199)

## Context

With `-u alice:pass --oauth2-bearer tok` against a server offering no OAuth mechanism, Curl
sent the password (`LOGIN` over IMAP, `APOP` or `USER`/`PASS` over POP3), and with
`--login-options AUTH=PLAIN` it refused to use PLAIN. Measured with
`Record-CurlExchange.ps1` against curl 8.22.0 (Schannel build), each with a token:

| Login options | Server offers | curl sends |
| --- | --- | --- |
| none | IMAP `PLAIN LOGIN DIGEST-MD5 CRAM-MD5` | nothing, exit 67 |
| `AUTH=PLAIN` | the same | `AUTHENTICATE PLAIN` |
| `AUTH=*` | the same and `XOAUTH2` | `AUTHENTICATE DIGEST-MD5` |
| `AUTH=*` | IMAP `PLAIN XOAUTH2` | `AUTHENTICATE XOAUTH2` |
| `AUTH=*` or `AUTH=+LOGIN` | IMAP, no `AUTH=` capability | `LOGIN` |
| none | POP3 `SASL PLAIN LOGIN`, APOP timestamp | nothing, exit 67 |
| none | POP3 `USER`, no SASL, no timestamp | nothing, exit 67 |
| `AUTH=*` | POP3 `USER`, timestamp | `APOP` |
| `AUTH=*` | POP3 `USER`, no timestamp | `USER`, `PASS` |
| `AUTH=+APOP` | POP3, timestamp | `APOP` |

SMTP gave the same answers for no option, `AUTH=PLAIN` and `AUTH=*`.

## Decision

- A bearer token makes OAUTHBEARER and XOAUTH2 the only preferred ways to log in. Any
  `AUTH=` login option replaces that preference, token or not. This holds for the SASL
  mechanisms and for `LOGIN`, `APOP` and `USER`/`PASS`.
- `SaslRequest.RequiredMechanism` carries the difference: `null` (no option) with a token
  keeps the password mechanisms from being chosen; `*` or a name lets them be. IMAP passes
  `*` whenever an `AUTH=` option was given, POP3 passes `*` for `AUTH=*`, and SMTP already
  passed the option's value.

This supersedes the part of ADR-0123 and ADR-0139 that said the password mechanisms are
never chosen with a token; what they measured was the case with no login option.

## Consequences

The password is no longer sent where curl sends nothing, and an explicit `AUTH=` choice is
honoured over IMAP, POP3 and SMTP.
