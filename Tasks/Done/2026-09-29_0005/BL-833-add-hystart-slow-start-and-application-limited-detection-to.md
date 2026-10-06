---
id: BL-833
title: Add HyStart++ slow start and application-limited detection to the QUIC CUBIC controller
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-725, BL-726]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-833 — Add HyStart++ slow start and application-limited detection to the QUIC CUBIC controller

## Goal

The QUIC client's slow start leaves early on a rising RTT as HyStart++ (RFC 9406) does, and neither CUBIC nor NewReno grows the congestion window while sending is limited by the application or flow control (RFC 9002 section 7.8), as curl's ngtcp2 build's CUBIC does.

## Context

- BL-725 built RFC 9002 loss detection, `QuicCongestionController`, `QuicCubicCongestionController` (RFC 9438, the default, ADR-0144 section 5) and `QuicNewRenoCongestionController` in `Curl.Quic.UnitLibrary`. Slow start there is RFC 9002's plain slow start, and every acknowledged packet sent outside recovery grows the window.
- ngtcp2's CUBIC (`lib/ngtcp2_cc.c`) runs HyStart++ in slow start and skips window growth while the sender is application-limited; read the ngtcp2 version curl.se's build uses before pinning numbers.
- Application-limited needs a sender with streams and flow control, which is BL-726; hence the dependency.
- Tests use `QuicLossRecoveryTests`' helpers (`Packet`, `Ms`) and a scripted RTT per round, as `QuicCubicCongestionControllerTests.RunRoundTrips` does.

## Acceptance criteria

- [x] `Curl.Quic.UnitTests` pin HyStart++'s rounds, its RTT-threshold exit into Conservative Slow Start, the CSS growth divisor and the return to slow start on a falling RTT, per RFC 9406 section 4.
- [x] `Curl.Quic.UnitTests` show that acknowledgements of packets sent while application- or flow-control-limited do not grow the window, for CUBIC and NewReno.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-725, which left both out to stay one task.
- Read ngtcp2's `lib/ngtcp2_cc.c` (main): `ngtcp2_cc_cubic_cc_on_ack_recv` and `ngtcp2_cc_reno_cc_on_pkt_acked`. Constants and ordering (grow, then round start, then sample, then CSS check) follow it; decisions in ADR-0198.
- Rounds are tracked by send time (a packet sent at or after the round started ends it), since the library has no rate sampler; one `OnPacketsAcknowledged` call is one acknowledgement and one `LatestRtt` sample, as ngtcp2 counts per ACK. The abstract `IncreaseWindow` now takes the acknowledgement's packets.
- HyStart++ runs only in the first slow start (RFC 9406 section 4.2's SHOULD); slow start after persistent congestion is plain.
- Application-limited is decided per flush in `QuicClientHandshake.Flush`: fewer bytes in flight sent than the allowance means the application or flow control ran out first. Probes and CONNECTION_CLOSE (window ignored) are never marked. CUBIC moves its epoch on by the time spent limited, as ngtcp2's `app_limited_duration`.
- `Receive_InMemoryServer_...` now pins that the handshake leaves the 12000-byte window unchanged: none of its flights fills the window.
- Touches widened to `Documentation/Planning/Decisions` for ADR-0198 and its README row; no task in Doing names it.
- Results: Curl.Quic.UnitTests 399 pass (17 new); Measure-CodeQuality 100% line, 100% branch, 0 failing members, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. QUIC CUBIC runs HyStart++ in its first slow start, and neither CUBIC nor NewReno grows the window for packets sent application- or flow-control-limited
