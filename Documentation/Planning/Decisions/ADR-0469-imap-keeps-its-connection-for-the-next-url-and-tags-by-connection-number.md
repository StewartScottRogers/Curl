# ADR-0469: IMAP keeps its connection for the next URL and tags by connection number

- Status: Accepted
- Date: 2026-10-10
- Task: BL-1987 (gap finding GF-0057)
- Decided by Claude under Stewart's delegation

## Context

curl 8.21.0 keeps an IMAP connection after a successful transfer and sends `LOGOUT` only from
`imap_disconnect`, when the connection cache closes it. The next URL for the same host, port
and login carries on with the next tag (`A005 FETCH ...`) and skips the `SELECT` when the
mailbox, and its UIDVALIDITY when both are known, is the one already selected (upstream tests
804, 815, 816 and 1982). Every tag starts with `'A' + connection_id % 26`, so the run's second
connection is tagged `B001` (tests 779 and 836). Curl logged out after every URL and always
tagged `A`.

## Decision

1. `ImapProtocolHandler` connects with `PoolScheme` set, as FTP does (ADR-0468). A successful
   transfer (fetch, listing, search, `-X` command or `APPEND`) hands the connection to the
   cache with an `ImapKeptConnection` holding the tag letter, the commands sent and the
   selected mailbox and UIDVALIDITY, instead of sending `LOGOUT`. Failures send `LOGOUT` and
   close, as before, as curl's `imap_done` closes a connection a transfer failed on.
2. A kept connection serves only the same user, password, login options (`;AUTH=` or
   `--login-options`) and `--ssl` level; one kept for another login is closed, with its
   `LOGOUT`, before a new connection is made, which puts test 836's `A005 LOGOUT` before
   `B001 CAPABILITY`.
3. A connection that `STARTTLS` upgraded is not kept and logs out as before: the secured
   wrapper is the session's, not the pool's, the same limit FTP keeps after `AUTH TLS`.
4. The tag letter is `'A' + ConnectResult.ConnectionNumber % 26`, the run's shared count.
5. The kept `LOGOUT` and its reply reach neither `-v`, `--trace` nor `-D`, as curl reports
   neither once the transfer is over; its wait for the tagged reply is 120 seconds.

## Consequences

Multi-URL IMAP command lines send what curl sends. Reusing a `STARTTLS` connection is left
out; a later task can carry the secured connection in the kept session if a test needs it.
