# ADR-0864 — An SSH session is kept on its connection for the next URL of the same login

- Status: Accepted
- Date: 2026-10-28
- Task: BL-3659 (CGR-130)
- Decided by Claude under Stewart's delegation.

## Context

curl keeps an `scp://` or `sftp://` connection in its connection cache after a successful
transfer and reuses it for the next URL of the command line: one TCP connection, one key
exchange, one authentication and, over SFTP, one `sftp` subsystem and one `REALPATH .` for the
whole command. `%{num_connects}` is 1 for the first URL and 0 for the next, `%{conn_id}` stays
0, `-v` writes `Reusing existing sftp: connection with host ...`, and the session ends once,
when the cache closes the connection, with the SFTP channel's close and `SSH_MSG_DISCONNECT`
11 `Shutdown`. Measured on 2026-10-28 with curl 8.18.0 (OpenSSL, Linux) and curl 8.18.0
(Windows, libssh2) against OpenSSH 10.2p1. Curl opened, authenticated and disconnected a new
connection for every URL.

## Decision

- After a successful transfer `SshProtocolHandler` leaves the authenticated session on its
  connection (`SshKeptSession`, through `IConnection.TryHoldSession`) and marks the connection
  reusable, so the run's pooling connector keeps it. A failed transfer, or a connection that
  cannot hold a session, ends the session with the channel's close and `DISCONNECT` as before.
- A reused connection whose session was authenticated with the same user name, password, key
  files and passphrase (`SshKeptSession.LoginKeyOf`), the fields curl's connection match
  compares for SSH, runs the transfer with no handshake and no authentication. A reused
  connection with another login, or with no SSH session, is closed and another one opened.
- Over SFTP the session and the home directory are kept on the connection
  (`SftpSessionSlot`): each transfer leaves the channel open, and the session's shutdown
  closes it before `DISCONNECT`.
- The transport reads and writes through `SshSessionConnection`, which is switched to the
  connection each transfer is given, so the transport's keys and sequence numbers carry over.
- A new connection that is kept reports `ConnectionCount` 1; a reused one reports none.

## Consequences

Several SSH URLs in one command cost one login, as with curl, and the server sees one session.
`%{num_connects}`, `%{conn_id}` and the `-v` connection lines match curl.
