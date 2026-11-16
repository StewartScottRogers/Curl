# ADR-1272 — A fresh connection ignores an unread FIN at its first write

Status: Accepted. Decided by Claude under Stewart's delegation. Amends ADR-0878.

## Context

AF-0808 (BL-5259): against a server that resets each connection right after accepting it, Curl on
Windows printed `Recv failure: Connection was aborted`; curl prints `Connection was reset`. Measured:
at the first write the socket was readable, `Available` was 0 and both a peeking and a plain receive
returned 0, so the pre-write check (ADR-0878) counted an unread FIN and kept the abort.

## Decision

The FIN part of the pre-write check applies only once the connection has read a byte (a reused kept
connection, ADR-0878). Before that, only unread bytes count (ADR-0708). Fixed reproduction: exit 56,
`Recv failure: Connection was reset`.

## Consequences

A server that closes with a FIN straight after accepting is worded as a reset on Windows; it was not
measured against curl separately.
