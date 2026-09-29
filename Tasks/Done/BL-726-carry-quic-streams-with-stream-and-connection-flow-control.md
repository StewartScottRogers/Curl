---
id: BL-726
title: Carry QUIC streams with stream and connection flow control
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-724]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests, Documentation/Planning/Decisions/ADR-0172-quic-streams-keep-fixed-flow-control-windows-and-one-loop-carries-the-connection.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-726 — Carry QUIC streams with stream and connection flow control

## Goal

The QUIC connection opens client-initiated bidirectional and unidirectional streams and accepts server-initiated ones, delivers each stream's bytes in order from out-of-order STREAM frames, ends and resets streams (FIN, RESET_STREAM, STOP_SENDING), enforces and advertises stream and connection flow control (MAX_DATA, MAX_STREAM_DATA, MAX_STREAMS, the BLOCKED frames), and exposes each stream through the multiplexed-connection contract of BL-721.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-724. References: RFC 9000 sections 2, 3 (stream states), 4 (flow control), 19.4 to 19.14. Initial limits are the transport parameters BL-718's ADR records.

## Acceptance criteria

- [x] `Curl.Quic.UnitTests` against the in-memory server reassemble a stream from reordered and duplicated frames, stop sending at the peer's limits and resume after MAX_DATA/MAX_STREAM_DATA, raise the client's limits as data is consumed, handle RESET_STREAM and STOP_SENDING, refuse to exceed MAX_STREAMS, and treat a flow-control violation by the peer as `FLOW_CONTROL_ERROR`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Plan: an I/O-free `QuicStreamSet` of `QuicStream`s inside `QuicClientHandshake`
  (`Streams`), with `QuicSendCredit` and `QuicReceiveCredit` for each limit; the
  application space's packets carry its frames. `QuicConnection` (with the internal
  `QuicMultiplexedStream`) implements BL-721's `IMultiplexedConnection` with one loop that
  owns the datagram channel. Decisions in ADR-0172 (fixed windows raised at half, as curl
  turns ngtcp2's window auto-tuning off; BLOCKED frames once per limit; STOP_SENDING
  answered by RESET_STREAM; `WriteAsync` completes once queued).
- `touches` gained ADR-0172 and the Decisions `README.md` index to record the decisions;
  no task in Doing names either.
- Found and fixed on the way: the datagram assembler coalesced two 1-RTT packets into one
  datagram, which a short header packet (no length field) cannot share.
- A post-handshake violation still fails with the handshake's exit 7; mapping close
  reasons to curl's exits is BL-727's.
- `QuicClientHandshake` now carries the connection beyond its handshake; ADR-0172 notes
  the name no longer says all it does (rename filed as a follow-up).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` 100% line, 100%
  branch, 525 members, 0 failing, worst CRAP 10. `Curl.Quic.UnitTests` 363 passing; the
  async connection tests passed 15 repeated runs.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. QUIC streams carry data both ways with RFC 9000 stream and connection flow control, and QuicConnection exposes them as IMultiplexedConnection
