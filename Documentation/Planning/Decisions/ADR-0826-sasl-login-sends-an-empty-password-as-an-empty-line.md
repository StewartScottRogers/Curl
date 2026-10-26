# ADR-0826 — SASL LOGIN sends an empty password as an empty line

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3632
- Decided by Claude under Stewart's delegation.

## Context

ADR-0754 gave `ISaslExchange` the member `SendsLastAnswerAsEmptyLine` and kept `LOGIN`'s empty
password as `=`, the explicit empty response curl 8.21.0 sent (and curl 8.18.0 on Linux still
sends). The third-party gap report's CGR-103 found that curl 8.22.0 sends it as an empty line.
Measured with `Record-CurlExchange.ps1` against curl 8.22.0 (Schannel, Windows) and
`-u user: --login-options AUTH=LOGIN`: SMTP and POP3 send `AUTH LOGIN`, `dXNlcg==`, then an
empty line; IMAP sends `A002 AUTHENTICATE LOGIN`, `dXNlcg==`, then an empty line.

## Decision

`ScriptedSaslExchange` (PLAIN, LOGIN, EXTERNAL, XOAUTH2, OAUTHBEARER) reports
`SendsLastAnswerAsEmptyLine` as `true`. LOGIN's password is the only answer a scripted exchange
can give empty, so this changes nothing else. The SMTP, POP3 and IMAP handlers already send an
empty answer as an empty line when the exchange says so (ADR-0754), so none of them changes.
We match the reference build, curl 8.22.0, on every platform; an empty initial response (an
empty user name) is still `=`.

## Consequences

- `-u user:` with LOGIN sends the same bytes as curl 8.22.0 over SMTP, POP3 and IMAP.
- Linux curl 8.18.0 still sends `=`; Curl follows 8.22.0 there too, as the newer reference.
