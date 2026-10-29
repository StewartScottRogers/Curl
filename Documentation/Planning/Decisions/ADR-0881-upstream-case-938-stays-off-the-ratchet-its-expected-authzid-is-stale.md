# ADR-0881 — Upstream case 938 stays off the ratchet: its expected `AUTH PLAIN` authorization identity is not what curl 8.22.0 sends

- Status: Accepted
- Date: 2026-10-23
- Task: BL-3399
- Amends: ADR-0737 (its row for case 938)
- Decided by Claude under Stewart's delegation.

## Context

ADR-0737 kept upstream case 938 (SMTP multiple connection authentication, listed in curl 8.22.0's
`tests/data/DISABLED`) off `PassingUpstreamCases.txt` and recorded Curl's difference as "the
`AUTH PLAIN` initial response is for the second user's credentials, not `user.one`'s". BL-3399
measured which side is right, by the standing rule of measuring the reference curl before pinning
output.

The case runs two SMTP uploads on one `-:` command line, `-u user.one:secret` for the first URL
and `-u user.two:secret` for the second, against a server that advertises only `AUTH PLAIN`. Its
`<protocol>` expects the PLAIN message with the user name as the authorization identity as well
(RFC 4616 `authzid NUL authcid NUL passwd`):

- first connection: `dXNlci5vbmUAdXNlci5vbmUAc2VjcmV0` (`user.one\0user.one\0secret`)
- second connection: `dXNlci50d28AdXNlci50d28Ac2VjcmV0` (`user.two\0user.two\0secret`)

## Measurement (2026-10-23, Windows)

`Record-CurlExchange.ps1 -Smtp -SmtpReply 'EHLO=250-localhost\r\n250 AUTH PLAIN','AUTH=334 PLAIN supported'`,
one session per user with the case's own arguments, run once with the reference curl 8.22.0
(`/ucrt64/bin/curl`, Schannel, ADR-0018) and once with Curl:

| Connection | curl 8.22.0 | Curl |
| --- | --- | --- |
| `-u user.one:secret` | `AUTH PLAIN`, then `AHVzZXIub25lAHNlY3JldA==` (`\0user.one\0secret`) | the same bytes |
| `-u user.two:secret` | `AUTH PLAIN`, then `AHVzZXIudHdvAHNlY3JldA==` (`\0user.two\0secret`) | the same bytes |

Through the case runner (`CURL_UPSTREAM_CASE=938`) Curl sends `\0user.one\0secret` on the first
connection (`EHLO 938001`) and `\0user.two\0secret` on the second (`EHLO 938002`): each connection
carries its own URL's credentials, so ADR-0737's "second user's credentials" description was wrong;
the only difference is the empty authorization identity. curl 8.22.0 sends an authorization identity
only when `--sasl-authzid` is given, as every enabled SMTP, IMAP and POP3 `SASL AUTH PLAIN` case
expects (test903: `%b64[%00user%00secret]b64%`).

## Decision

Case 938 stays off `PassingUpstreamCases.txt`: its expected output does not match curl 8.22.0 itself,
which is why upstream lists it in `DISABLED`. Curl already sends curl 8.22.0's bytes on both
connections, so Curl is not changed. Making the case pass would mean sending an authorization
identity curl does not send, which would break the drop-in rule and the enabled PLAIN cases.

## Consequences

- ADR-0737's row for 938 is superseded by this measurement; its other rows stand.
- If a later upstream release corrects case 938's expected bytes to `%00user.one%00secret`, the case
  can be run again and listed after ten passing runs.
