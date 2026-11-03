# ADR-1205 — The AMQP 1.0 client sends an empty frame every half of the peer's idle-time-out

- **Status:** Accepted
- **Date:** 2026-11-02

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-4586.

## Context

OASIS AMQP 1.0 Part 2 section 2.4.5 lets a peer's open carry an idle-time-out and closes a connection that stays
silent for longer; it suggests sending at least every half of it, an empty frame when there is nothing else to send.
BL-4528's conformance case `Amqp1ConnectionCases.IdleTimeout_ContainerAsks1000msAndWaits2500_CurlSendsEmptyFrames`
found that `Amqp1ClientFlow` sent none. Upstream curl has no AMQP, so the specification decides.

## Decision

- When the peer's open carries an idle-time-out greater than 0, `Amqp1ClientFlow` starts one loop that sends an empty
  frame every half of it, timed on the transfer's injected `TimeProvider`, until the exchange ends; a missing or zero
  idle-time-out starts none.
- The loop sends on a fixed period instead of only after a quiet half-period: the spec asks only for "at least" that
  often, an empty frame costs 8 bytes, and the fixed period needs no record of when the last frame went out.
- `Amqp1FrameChannel` serialises its writes, so an empty frame never interleaves with another frame's bytes.
- The loop stops when the exchange ends, success or failure, and the exchange's own outcome is the transfer's. A
  failed empty-frame write reports as the send failure it is.

## Consequences

A quiet wait (for credit, a disposition or a message) no longer lets the peer close the connection under it. The
unit tests pin the period on a hand-made fake clock.
