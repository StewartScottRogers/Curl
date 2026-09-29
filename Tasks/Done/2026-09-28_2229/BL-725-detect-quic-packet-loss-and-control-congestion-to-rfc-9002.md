---
id: BL-725
title: Detect QUIC packet loss and control congestion to RFC 9002
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-724]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-725 — Detect QUIC packet loss and control congestion to RFC 9002

## Goal

The QUIC connection acknowledges packets (ACK frames with ranges and ACK delay), estimates RTT, detects loss by packet and time thresholds, runs the probe timeout, retransmits lost frames' data (never packets), paces and limits sending with the congestion controller BL-718's ADR records curl's build using, all per RFC 9002 on `TimeProvider`.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-724. References: RFC 9002 sections 5 (RTT), 6 (loss detection, PTO), 7 (NewReno, which is the RFC's; if the ADR records CUBIC (RFC 9438) as ngtcp2's default for curl, implement that as well and use it by default), Appendix A and B (pseudocode).
- Tests use a fake datagram channel that drops, delays and reorders on a script, with a fake `TimeProvider`.

## Acceptance criteria

- [x] `Curl.Quic.UnitTests` pin RTT estimates for a scripted ACK sequence, declare loss by packet threshold and by time threshold as Appendix A computes, fire PTO with exponential backoff, retransmit the lost CRYPTO and STREAM data in new packets, and show the congestion window's growth, reduction on loss, and recovery period.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Delivered (2026-09-28): `QuicRttEstimator` (section 5), `QuicLossRecovery` (Appendix A, told the time on every call), `QuicSentPacket`, `QuicPacketNumberSpaceId`, `QuicCongestionController` with `QuicCubicCongestionController` (RFC 9438, the default, as ADR-0144 section 5 records for curl's ngtcp2 build) and `QuicNewRenoCongestionController` (Appendix B), and `QuicPacer` (section 7.7). `QuicClientHandshake` takes a `TimeProvider`, records every packet sent, resends lost data in new packets, probes on the PTO and exposes the timer and pacing to `QuicClientConnector`, which runs both. ACK frames now carry the ACK Delay (exponent 3). The assembler stops at the congestion window and starts a new datagram when the destination connection ID changes (the BL-724 note above).
- Tests: `QuicRttEstimatorTests` (scripted samples), `QuicLossRecoveryTests` (packet and time thresholds, PTO 999/2997/6993/14985 ms, anti-deadlock probe, application data waits for confirmation, persistent congestion), `QuicNewRenoCongestionControllerTests` and `QuicCubicCongestionControllerTests` (growth, reduction, recovery period, fast convergence, the cubic's concave-then-convex shape), `QuicPacerTests`, `QuicPacketNumberSpaceTests` (CRYPTO and STREAM resent, split), `QuicDatagramAssemblerTests`, and handshake and connector tests where a dropped first Initial is probed and resent and the handshake still completes. Quic: 307 tests; Measure-CodeQuality: 100% line, 100% branch, 0 failing members.
- Choices taken as sensible defaults (rule 1), within ADR-0144's decision, so no new ADR:
  - One probe packet per probe timeout (RFC 9002 section 6.2.4 allows one or two); it resends the oldest unacknowledged ack-eliciting packet's data, else a PING.
  - The ACK Delay counts only in the application data space; Initial and Handshake acknowledgements are not deliberately delayed (section 5.3 allows ignoring it), as ngtcp2 does.
  - Persistent congestion is judged within the losses one call declares, where a run is consecutive packet numbers: conservative, never declares it wrongly.
  - Acknowledgement-only packets, probes and CONNECTION_CLOSE are not held by the congestion window; a packet that starts inside the window may end past it.
  - The pacer is a token bucket of 10 datagrams filling at 1.25 x cwnd / smoothed RTT; an RTT under the 1 ms granularity counts as 1 ms. `QuicClientConnector` waits on it before each datagram.
  - A Retry discards the Initial space's recovery state (section 6.3).
- `QuicTestServer` answered a retransmitted ClientHello a second time with fresh keys; it now answers only when new CRYPTO bytes arrive.
- Follow-up filed: BL-833 (HyStart++ and application-limited detection in CUBIC, after BL-726).

- From BL-724 (ADR-0165 decision 7): `QuicDatagramAssembler` coalesces Handshake and 1-RTT packets even when they go to different destination connection IDs (the server retired its handshake ID before HANDSHAKE_DONE), which RFC 9000 section 12.2 forbids. When retransmission reworks sending, start a new datagram when the destination connection ID changes.

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. QUIC detects loss by packet and time thresholds, runs the probe timeout with backoff, resends lost CRYPTO and STREAM data in new packets, and paces and limits sending with CUBIC (NewReno available) per RFC 9002
