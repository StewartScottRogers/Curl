---
id: BL-6072
title: Print -v's Connection timed out after line when -m ends a QUIC dial or a UDP proxy tunnel on Linux
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6039]
touches: [Curl.Networking.UnitLibrary/TcpConnector.UdpTunnel.cs, Curl.Networking.UnitLibrary/QuicDialer.cs, Curl.Networking.UnitTests/QuicTimedOutLineTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6072 — Print -v's Connection timed out after line when -m ends a QUIC dial or a UDP proxy tunnel on Linux

## Goal

A QUIC dial or a CONNECT-UDP proxy tunnel that `-m` ends on Linux prints `-v`'s `Connection timed out after N milliseconds` line, as a TCP connect does since BL-6039.

## Context

`TcpConnector.UdpTunnel.cs` and `QuicDialer.cs` report the line only once their own clock reads the limit, so on Linux, where `-m`'s cancellation arrives while the connect's limit still has its start-up time to run, the cancellation escapes without it. ADR-1457 gives the rule `TcpConnector.IsEndedByConnectLimit` applies; apply the same one. Measure real curl with `--http3-only` before pinning text.

## Acceptance criteria

- [ ] A unit test pins the line for a QUIC dial cancelled just before its limit and for a UDP proxy tunnel cancelled just before its limit.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 73f0b96d 3d836d5e.
