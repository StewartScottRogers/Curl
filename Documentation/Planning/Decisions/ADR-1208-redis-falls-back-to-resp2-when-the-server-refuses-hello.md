# ADR-1208: Redis falls back to RESP2 when the server refuses HELLO

- Status: Accepted
- Date: 2026-11-02
- Decided by Claude under Stewart's delegation (BL-4601)

## Context

ADR-1079's Redis client opens every connection with `HELLO 3` (with `AUTH user password` when there are credentials)
and ended with exit 8 on any error reply. A server older than Redis 6 has no `HELLO` and answers
`-ERR unknown command 'HELLO'`. The RESP3 specification ("The HELLO command and connection handshake") says a client
that gets an error to `HELLO` goes on in RESP2. Without `HELLO`'s `AUTH`, a RESP2 client authenticates with the
`AUTH` command, which before Redis 6 takes only the password.

## Decision

- An error reply to `HELLO` whose code is `NOAUTH` or `WRONGPASS` ends with exit 67 (login denied), as before.
- Any other error reply to `HELLO` means the server does not speak RESP3: the client goes on in RESP2.
- With credentials, the RESP2 client then sends `AUTH password` when no user name is given (the only form a server
  older than Redis 6 takes) and `AUTH user password` when one is. An error reply to it ends with exit 67, its message
  `Redis server answered AUTH with error: <text>`.
- Then `SELECT` and the command, as in RESP3, its reply read as below.

## Consequences

`redis://` works against pre-6 servers, with and without credentials. An error to `HELLO` with credentials other
than `WRONGPASS` or `NOAUTH` no longer ends with exit 67 by itself: the `AUTH` that follows decides.

A RESP2 null array (`*-1`) is read as an array with no elements, so it renders as nothing (`Redis:RESP2-Null-2`),
while a null bulk string (`$-1`) stays a null and renders as an empty line (`Redis:RESP2-Null-1`).
