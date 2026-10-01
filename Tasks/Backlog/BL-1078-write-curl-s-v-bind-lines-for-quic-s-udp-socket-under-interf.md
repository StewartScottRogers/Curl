---
id: BL-1078
title: Write curl's -v bind lines for QUIC's UDP socket under --interface and --local-port
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1027]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1078 — Write curl's -v bind lines for QUIC's UDP socket under --interface and --local-port

## Goal

`curl --http3-only -v` with `--interface` or `--local-port` writes the bind lines curl's ngtcp2 build writes for its UDP socket, as the TCP path does since BL-1027.

## Context

- Follow-up from BL-1027 (ADR-0294, Decision 4). TCP dials write `Name ... resolved to`, `Local port: N`,
  `Bind to local port N failed, trying next`, `bind failed with errno N: ...` and the `Could not ...` lines
  through `LocalBindLines`. QUIC's `QuicDialer.OpenBoundChannelAsync` passes `NoTransferEvents.Instance` to
  `LocalBindingAddressChooser.ChooseAsync`, and `UdpChannelOpener.OpenFrom` passes it to `TcpDialer.BindLocalEnd`,
  because ADR-0292 measured only one `Failed to connect to` line and the bind lines were never measured over QUIC.
- Measure first with curl.se's ngtcp2 build (as BL-1025 did): `--http3-only -v --interface 127.0.0.1 --local-port 40010-40012`,
  a busy port range, `--interface bogus0`, `--interface ::1` to an IPv4 address. If curl writes no bind line for QUIC,
  record that in ADR-0294's consequences and close the task with a test pinning the silence.

## Acceptance criteria

- [ ] Each case above is measured with curl's ngtcp2 build and its `-v` lines pinned in `TcpConnectorQuicTests.LocalBinding`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-10-01: Created.
