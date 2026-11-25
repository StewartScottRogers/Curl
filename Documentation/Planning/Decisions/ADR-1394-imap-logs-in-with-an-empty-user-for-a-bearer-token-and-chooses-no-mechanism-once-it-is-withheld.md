# ADR-1394: IMAP logs in with an empty user for a bearer token, and chooses no mechanism once it is withheld

- Status: Accepted
- Date: 2026-11-25
- Decided by Claude under Stewart's delegation
- Task: BL-5961

## Context

ADR-1361 left two IMAP cases: `--oauth2-bearer` without `-u`, and a redirect that withholds
the token (`MailRequestOptions.BearerTokenWithheld`). Measured with
`Record-CurlExchange.ps1 -Imap` against curl 8.22.0 (Schannel build), each with
`--oauth2-bearer tok` and no `-u`; "redirect" is an HTTP 302 to the IMAP URL, followed with
`-L --proto-redir +imap`:

| Case | Login options | Server offers | curl sends after `CAPABILITY` | Exit |
| --- | --- | --- | --- | --- |
| direct | `AUTH=PLAIN`, `AUTH=LOGIN` | the named mechanism | `AUTHENTICATE` and an empty user and password | 0 |
| direct | `AUTH=` naming CRAM-MD5, DIGEST-MD5, NTLM, EXTERNAL, XOAUTH2 or OAUTHBEARER | the named mechanism | `AUTHENTICATE <name>`, answered `OK` with no continuation | 67 |
| direct | none | PLAIN, LOGIN, CRAM-MD5, DIGEST-MD5, NTLM, EXTERNAL or nothing | nothing | 67 |
| direct | none | XOAUTH2 or OAUTHBEARER | `AUTHENTICATE <name>`, answered `OK` with no continuation | 67 |
| direct | `AUTH=+LOGIN` | PLAIN | `LOGIN` with two empty arguments | 0 |
| redirect to `v@` | none | PLAIN, LOGIN, CRAM-MD5, EXTERNAL or XOAUTH2 | nothing | 67 |
| redirect to `v@` | `AUTH=PLAIN` | PLAIN | `AUTHENTICATE PLAIN` for `v` | 0 |
| redirect to `v:p@` | none | PLAIN or nothing | nothing | 67 |
| redirect, no user | none | PLAIN | no login; `LIST` | 0 |

## Decision

- A bearer token without a credential gives the SASL request an empty user and password, so
  the mechanisms are chosen as curl chooses them.
- When a redirect withheld the token and no `AUTH=` option is given, no SASL mechanism is
  chosen. An `AUTH=` option replaces the bearer preference, as in ADR-1361.

## Consequences

Curl sends the same IMAP commands and exits with the same code as curl in every row. POP3
and SMTP are BL-5962 and BL-5963.
