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
completed:
---
# BL-725 — Detect QUIC packet loss and control congestion to RFC 9002

## Goal

The QUIC connection acknowledges packets (ACK frames with ranges and ACK delay), estimates RTT, detects loss by packet and time thresholds, runs the probe timeout, retransmits lost frames' data (never packets), paces and limits sending with the congestion controller BL-718's ADR records curl's build using, all per RFC 9002 on `TimeProvider`.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Builds on BL-724. References: RFC 9002 sections 5 (RTT), 6 (loss detection, PTO), 7 (NewReno, which is the RFC's; if the ADR records CUBIC (RFC 9438) as ngtcp2's default for curl, implement that as well and use it by default), Appendix A and B (pseudocode).
- Tests use a fake datagram channel that drops, delays and reorders on a script, with a fake `TimeProvider`.

## Acceptance criteria

- [ ] `Curl.Quic.UnitTests` pin RTT estimates for a scripted ACK sequence, declare loss by packet threshold and by time threshold as Appendix A computes, fire PTO with exponential backoff, retransmit the lost CRYPTO and STREAM data in new packets, and show the congestion window's growth, reduction on loss, and recovery period.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Quic.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
