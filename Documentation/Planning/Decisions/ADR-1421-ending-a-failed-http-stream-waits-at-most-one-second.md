# ADR-1421 — Ending a failed HTTP stream waits at most one second

- **Status:** Accepted
- **Date:** 2026-11-26

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-6006.

## Context

A failed or timed-out HTTP/2 or HTTP/3 transfer ends its stream so the connection can carry
the next one. By then the transfer's token and its time limits no longer apply (BL-5998), and on
HTTP/2 the end is a RST_STREAM written to the shared connection. A peer that has stopped
reading leaves that write, or the wait for the session's write lock, with nothing to stop it:
the process hangs instead of exiting.

## Decision

- The cleanup of a failed exchange and of a timed-out one runs under its own token, which
  cancels after one second on the transfer's `TimeProvider`. Only that cancellation is caught;
  the transfer then ends with its own exit code.
- An HTTP/2 session whose write was cancelled part way writes nothing more when it closes: no
  RST_STREAM and no GOAWAY. It already takes no new stream.

## Why

- A healthy connection takes a 13-byte frame at once, so one second only passes when the peer
  is not reading. It is short enough that `-m` and `-f` still end promptly.
- The bytes after a part-written frame cannot be parsed by the peer, and writing them is what
  would hang the close, which the connection pool runs without a token.
