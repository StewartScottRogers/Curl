# ADR-0833: IMAP holds its greeting and login to `--connect-timeout`

- Status: Accepted
- Date: 2026-10-26
- Task: BL-3882
- Decided by Claude under Stewart's delegation.

## Context

libcurl counts every state before `DO` (the TCP connect, then the protocol's own connect phase)
against `--connect-timeout`, from the start of the request. An IMAP server that accepts and never
greets therefore ends a curl transfer after the limit. Measured with curl 8.21.0 Schannel on
Windows against a loopback listener that accepts and stays silent:
`curl -sS -w '%{exitcode}|%{errormsg}\n' imap://127.0.0.1:PORT/ --connect-timeout 2` exits 28
after about 2 s with `curl: (28) Operation timed out after 2013 milliseconds with 0 bytes received`.
Curl read the greeting on the transfer's token alone and never exited (CGR-003).

## Decision

`ImapConnectPhaseLimit` holds the IMAP session's connect phase (the greeting, `CAPABILITY`,
`STARTTLS` with its handshake, and the login) to `--connect-timeout`, or curl's
`DEFAULT_CONNECT_TIMEOUT` of 300 s when the option is missing or 0. The limit counts from the start
of the request on the context's `TimeProvider`. While the session opens, `ImapControlChannel`
sends and reads on the limit's token. When the limit passes, the transfer ends with exit 28 and
curl's `Operation timed out after N milliseconds with 0 bytes received`, not its
`Connection timed out` text, because the TCP connect is over. Once the session is open, the
channel goes back to the transfer's own token, so a slow `FETCH` is held only by `--max-time`.

## Consequences

- IMAP matches curl against a server that never greets or never answers during login.
- POP3 (BL-3881) and SMTP (BL-3883) need the same change in their own libraries.
