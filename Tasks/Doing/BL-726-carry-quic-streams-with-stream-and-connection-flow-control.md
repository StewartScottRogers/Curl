---
id: BL-726
title: Carry QUIC streams with stream and connection flow control
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-724]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-726 — Carry QUIC streams with stream and connection flow control

## Goal

The QUIC connection opens client-initiated bidirectional and unidirectional streams and accepts server-initiated ones, delivers each stream's bytes in order from out-of-order STREAM frames, ends and resets streams (FIN, RESET_STREAM, STOP_SENDING), enforces and advertises stream and connection flow control (MAX_DATA, MAX_STREAM_DATA, MAX_STREAMS, the BLOCKED frames), and exposes each stream through the multiplexed-connection contract of BL-721.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-724. References: RFC 9000 sections 2, 3 (stream states), 4 (flow control), 19.4 to 19.14. Initial limits are the transport parameters BL-718's ADR records.

## Acceptance criteria

- [ ] `Curl.Quic.UnitTests` against the in-memory server reassemble a stream from reordered and duplicated frames, stop sending at the peer's limits and resume after MAX_DATA/MAX_STREAM_DATA, raise the client's limits as data is consumed, handle RESET_STREAM and STOP_SENDING, refuse to exceed MAX_STREAMS, and treat a flow-control violation by the peer as `FLOW_CONTROL_ERROR`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
