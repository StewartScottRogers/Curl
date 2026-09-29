---
id: BL-833
title: Add HyStart++ slow start and application-limited detection to the QUIC CUBIC controller
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-725, BL-726]
touches: [Curl.Quic.UnitLibrary, Curl.Quic.UnitTests]
requirement: none
created: 2026-09-28
completed:
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

- [ ] `Curl.Quic.UnitTests` pin HyStart++'s rounds, its RTT-threshold exit into Conservative Slow Start, the CSS growth divisor and the return to slow start on a falling RTT, per RFC 9406 section 4.
- [ ] `Curl.Quic.UnitTests` show that acknowledgements of packets sent while application- or flow-control-limited do not grow the window, for CUBIC and NewReno.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

- Filed by BL-725, which left both out to stay one task.

## Log

- 2026-09-28: Created.
