# ADR-1433 — Closing an HTTP/2 connection waits at most one second

- **Status:** Accepted
- **Date:** 2026-11-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6036.

## Context

ADR-1421 stops an HTTP/2 session writing its closing frames after an earlier write was
cancelled. A peer may also stop reading only when the connection closes. The connection pool
and the handler close a session without a token, so the RST_STREAM, the GOAWAY, the flush or
the wait for the session's write lock then waits for ever.

## Decision

- `Http2Session.ShutDownAsync` runs under its own limit of one second on the session's
  `TimeProvider`, linked to the caller's token. The limit covers the wait for the write lock,
  each RST_STREAM, the GOAWAY and the flush.
- When the limit passes, the rest of the closing frames is given up and the close succeeds.
- The handler creates each session, the h2c upgrade's too, with the transfer's
  `TimeProvider`. A pooled session keeps the clock it was created with.

## Why

- The same one second as ADR-1421, for the same reason: a healthy connection takes these few
  bytes at once, so the limit only passes when the peer is not reading.
- The session is what writes the frames, so bounding it there covers every caller, the pool
  included, without changing `IConnectionSession`.
