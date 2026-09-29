# ADR-0174 — QUIC streams keep fixed flow control windows, and one loop carries the connection

- **Status:** Accepted
- **Date:** 2026-09-28

Decided by Claude under Stewart's delegation (root `CLAUDE.md`, "Decisions"), in BL-726.

## Context

BL-726 adds streams and flow control (RFC 9000 sections 2 to 4) to `Curl.Quic.UnitLibrary`
and exposes each stream through BL-721's `IMultiplexedConnection` and
`IMultiplexedStream`. The initial limits are the transport parameters ADR-0144 section 5
records from curl's ngtcp2 build (`initial_max_stream_data_bidi_local` 32 768,
`initial_max_data` 1 048 576 000, 262 144 streams of each type). RFC 9000 leaves open
when a receiver raises its limits, what a sender does once blocked, and how an I/O-free
state machine meets callers that read, write and open streams concurrently.

## Decision

1. **Fixed windows, raised at half.** curl sets ngtcp2's `max_stream_window` to 0
   (ADR-0144), which turns stream window auto-tuning off, so every limit keeps its initial
   size as its window. Once less than half the window is left above what the application
   has consumed, the new limit is what was consumed plus the window: `MAX_STREAM_DATA` per
   stream, `MAX_DATA` for the connection, and `MAX_STREAMS` for the server's streams,
   where a stream counts as consumed once both its parts are done. No limit is raised on a
   stream whose final size is known. `QuicReceiveCredit` holds this rule.
2. **Blocked once per limit.** `DATA_BLOCKED`, `STREAM_DATA_BLOCKED` and
   `STREAMS_BLOCKED` go out once for each limit the client runs into (`QuicSendCredit`),
   in the packet whose data reached the limit when they fit.
3. **Frames the peer sends are checked as RFC 9000 says.** Past a limit is
   `FLOW_CONTROL_ERROR` or `STREAM_LIMIT_ERROR`; a frame for a part the stream does not
   have, or for a client stream not opened, `STREAM_STATE_ERROR`; a changed or exceeded
   final size `FINAL_SIZE_ERROR`. The connection closes with that error; mapping it to
   curl's exit is BL-727's (for now it is the handshake's exit 7).
4. **STOP_SENDING is answered with RESET_STREAM** carrying the same code and the offset
   sent so far; the unsent bytes are dropped, and lost STREAM data of a reset stream is
   not sent again. `Abort` sends `RESET_STREAM` and `STOP_SENDING`, each only where it
   still applies; what arrives afterwards counts as consumed, so the connection's credit
   still moves.
5. **The state stays I/O-free.** `QuicStreamSet` and `QuicStream` live inside
   `QuicClientHandshake` (`Streams`); stream frames ride the 1-RTT packets after the
   control frames. A short header packet always ends its datagram, since it has no length.
6. **One loop owns the channel.** `QuicConnection` implements `IMultiplexedConnection`:
   a single loop sends what is queued, receives, and runs the loss detection timer.
   Readers, writers and openers change the streams under one lock, wake the loop, and wait
   on a signal the loop raises after each datagram. `WriteAsync` completes once the bytes
   are queued; flow control decides when they go. A lost channel is exit 56; `CloseAsync`
   sends an application `CONNECTION_CLOSE`, and `DisposeAsync` closes with code 0 if it has
   not been closed.

## Consequences

- Behaviour matches curl's ngtcp2 build as configured, with no auto-tuning to reproduce.
- The server's bidirectional streams are created and kept (`AcceptBidirectional`), although
  the contract offers only unidirectional accepts, which is all HTTP/3 needs.
- `QuicClientHandshake` now carries the connection past its handshake; its name no longer
  says all it does, and a later alignment task may rename it.

## Alternatives considered

- **Window auto-tuning as ngtcp2 can do.** Rejected: curl turns it off.
- **A write that waits for flow control credit.** Rejected: the contract says a write
  completes when the stream accepts the bytes, and HTTP/3 bodies are bounded by the
  transfer, so buffering them is the simpler drop-in.
- **Callers driving I/O themselves.** Rejected: concurrent streams would race on the
  channel; one loop keeps the engine single-threaded behind a lock.
