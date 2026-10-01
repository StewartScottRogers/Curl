---
id: BL-1077
title: Bind QUIC's UDP socket to an --interface device with SO_BINDTODEVICE on Linux
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1026]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1077 — Bind QUIC's UDP socket to an --interface device with SO_BINDTODEVICE on Linux

## Goal

On Linux, QUIC's UDP socket binds an `--interface` name to its device with `SO_BINDTODEVICE` before any address, as the TCP path does since BL-1026: a plain or `if!` name bound so binds nothing else, `ifhost!` goes on to bind its host.

## Context

- Follow-up from BL-1026 (ADR-0293, Consequences). libcurl's `bindlocal` (`lib/cf-socket.c`) runs for
  every IP socket, QUIC's included; ADR-0292 binds the UDP socket's address through
  `LocalBindingAddressChooser` and `IUdpChannelOpener.OpenFrom` but takes no device step.
- Reuse `TcpDialer.TryBindToDevice` (Linux only, excluded per ADR-0083) and mirror the decision in
  `LocalBindingTcpDialer.DialAsync`, keeping it testable through `IUdpChannelOpener`.
- Measure on Linux (WSL has curl 8.18.0; a QUIC-capable build may be needed) with `-v`.

## Acceptance criteria

- [x] `Curl.Networking.UnitTests` pin the device bind asked for, and that a plain or `if!` name bound so binds no address or port while `ifhost!` binds its host after it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- Plan: `IUdpChannelOpener.OpenFromDeviceAsync` mirrors `ITcpDialer.DialFromDeviceAsync`; `QuicDialer`
  calls it whenever `InterfaceName ?? DeviceName` is set, with `bindsAddressAfterDevice` true only for
  `ifhost!`, exactly as `LocalBindingTcpDialer.DialAsync` decides. `UdpChannelOpener` runs the covered
  `TcpDialer.BindDeviceOrLocalEndAsync` on a new UDP socket with `TcpDialer.TryBindToDevice` (an internal
  constructor takes the device bind so the Windows coverage run reaches the device-bound branch), then
  hands the socket to a new internal `UdpDatagramChannel(serverEndPoint, boundSocket)` constructor.
- Choice: a socket bound to its device alone is then bound to the any address on port 0. libcurl leaves it
  unbound and the kernel binds it on the first `sendto`/`connect`, which is the same end; .NET refuses
  `ReceiveFrom` on an unbound socket, so the explicit bind keeps the channel usable either way.
- Measurement: WSL's curl 8.18.0 has no HTTP/3 (no ngtcp2 in `curl -V`), so QUIC's `-v` output under
  `--interface` could not be measured. The behaviour follows libcurl's source (`bindlocal` in
  `lib/cf-socket.c` runs for every IP socket, QUIC's included) and ADR-0293. As before (ADR-0292) QUIC
  writes none of the bind's `-v` lines, the device line included, since they are unmeasured; no ADR was
  needed beyond ADR-0293's stated consequence.
- Tests: four `ConnectMultiplexedAsync_*` tests pin the device bind asked for and what is bound after it;
  six `UdpChannelOpener_OpenFromDeviceAsync_*` tests pin the real socket's binding (one Linux-only with `lo`).

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. QUIC's UDP socket binds an --interface name to its device with SO_BINDTODEVICE on Linux before any address, as TCP does
