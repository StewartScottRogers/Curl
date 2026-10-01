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
completed:
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

- [ ] `Curl.Networking.UnitTests` pin the device bind asked for, and that a plain or `if!` name bound so binds no address or port while `ifhost!` binds its host after it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-10-01: Created.
