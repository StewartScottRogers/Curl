# ADR-1397: POP3 logs a bearer token without a user in with an empty credential, and counts a withheld token

- Status: Accepted
- Date: 2026-11-25
- Decided by Claude under Stewart's delegation
- Task: BL-5962 (found in BL-5880)

## Context

ADR-1361 measured `--oauth2-bearer` with `-u`. Two cases were left: no `-u`, and a token a
redirect withheld (`MailRequestOptions.BearerTokenWithheld`). Measured over POP3 with
`Record-CurlExchange.ps1` against curl 8.22.0 (Schannel build):

| Case | Login options | curl does |
| --- | --- | --- |
| no `-u` | none | chooses only a bearer mechanism |
| no `-u` | `AUTH=PLAIN`, `AUTH=LOGIN` | that mechanism, empty user and password |
| no `-u` | `AUTH=*`, no SASL mechanism usable | `APOP` or `USER`/`PASS`, empty user and password |
| no `-u` | `AUTH=+APOP` | `APOP` with an empty user |
| redirect to `pop3://v@host` | none | nothing after `CAPA`, exit 67 |
| redirect to `pop3://v@host` | `AUTH=PLAIN`, `AUTH=*` | logs in as `v` with an empty password |
| redirect to `pop3://host` | none, `AUTH=PLAIN`, `AUTH=*` | no login; sends `RETR` |

## Decision

`Pop3Login` supplies the empty user and password itself when there is a token and no user,
because the rule is POP3's login order and the shared SASL ranking keeps requiring a
credential. A withheld token still makes the bearer mechanisms the only preferred ways to
log in until an `AUTH=` login option replaces that preference; with no token to use them,
no SASL mechanism, `APOP` or `USER`/`PASS` is chosen.

## Consequences

Curl sends the same commands, `SASL:` lines and exit codes as curl in the cases above, and
no password follows a redirect that withheld the token unless an `AUTH=` option asks for it.
