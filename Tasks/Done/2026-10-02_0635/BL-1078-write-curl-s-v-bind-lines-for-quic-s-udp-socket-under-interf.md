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
completed: 2026-10-02
---
# BL-1078 — Write curl's -v bind lines for QUIC's UDP socket under --interface and --local-port

## Goal

`curl --http3-only -v` with `--interface` or `--local-port` writes the bind lines curl's ngtcp2 build writes for its UDP socket, as the TCP path does since BL-1027.

## Context

- Follow-up from BL-1027 (ADR-0295, Decision 4). TCP dials write `Name ... resolved to`, `Local port: N`,
  `Bind to local port N failed, trying next`, `bind failed with errno N: ...` and the `Could not ...` lines
  through `LocalBindLines`. QUIC's `QuicDialer.OpenBoundChannelAsync` passes `NoTransferEvents.Instance` to
  `LocalBindingAddressChooser.ChooseAsync`, and `UdpChannelOpener.OpenFrom` passes it to `TcpDialer.BindLocalEnd`,
  because ADR-0292 measured only one `Failed to connect to` line and the bind lines were never measured over QUIC.
- Measure first with curl.se's ngtcp2 build (as BL-1025 did): `--http3-only -v --interface 127.0.0.1 --local-port 40010-40012`,
  a busy port range, `--interface bogus0`, `--interface ::1` to an IPv4 address. If curl writes no bind line for QUIC,
  record that in ADR-0295's consequences and close the task with a test pinning the silence.

## Acceptance criteria

- [x] Each case above is measured with curl's ngtcp2 build and its `-v` lines pinned in `TcpConnectorQuicTests.LocalBinding`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- Measured: BL-1025's Notes already hold every case this task names, run with curl.se's 8.18.0 LibreSSL/ngtcp2
  Windows build through `Record-CurlExchange.ps1 -UdpSink`: curl writes the TCP path's bind lines for its UDP
  socket (`Name ... resolved to`, `Local port: N`, `Bind to local port N failed, trying next`,
  `bind failed with errno 10048`, `Could not resolve host` / `Could not bind to`). Not re-measured.
- Decision (ADR-0352, decided by Claude under Stewart's delegation; amends ADR-0295 Decision 4):
  `IUdpChannelOpener.OpenFrom`/`OpenFromDeviceAsync` take `ITransferEvents`; `QuicDialer` passes the
  target's `Events` to them and to `LocalBindingAddressChooser.ChooseAsync`. A plain parameter, not a
  default interface method: the only implementations are `UdpChannelOpener` and the test fake.
- Pinned in `TcpConnectorQuicTests.LocalBinding`: the bound case, `if!bogus0`, plain `bogus0` (bind host
  named, per ADR-0295 Decision 3), `::1` to IPv4, a busy port then the next, every port busy.
  `FakeDnsResolver` gained `HostsWithNoAddress`; `QuicServerChannelOpener` writes `Local port: N`.
- `Measure-CodeQuality.ps1` flagged `TcpConnector`'s constructor at complexity 12 (BL-1053's `dnsCache ??`);
  `HttpOverTlsApplicationProtocols` became an expression-bodied property, bringing it to 10. Now 100% line,
  100% branch, 0 failing members.
- `--ai-help`: no option added or changed.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. curl --http3 -v with --interface/--local-port writes curl's bind lines for QUIC's UDP socket
