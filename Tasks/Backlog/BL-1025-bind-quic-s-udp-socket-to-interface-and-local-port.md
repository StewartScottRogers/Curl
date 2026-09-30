---
id: BL-1025
title: Bind QUIC's UDP socket to --interface and --local-port
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-600]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-1025 — Bind QUIC's UDP socket to --interface and --local-port

## Goal

An `--http3` or `--http3-only` transfer binds its UDP socket as `--interface` and `--local-port` ask, as libcurl's `bindlocal` binds every IP socket, with the same exit 45 and exit 43 failures as TCP.

## Context

- Follow-up from BL-600 (ADR-0268, Consequences): `TcpConnector` binds TCP dials through
  `LocalBindingTcpDialer`, but `ConnectMultiplexedAsync` hands addresses to `QuicDialer`, whose
  `IUdpChannelOpener` (`UdpChannelOpener`) binds its own local address and port.
- Reuse `LocalBinding` and the address choice in `LocalBindingTcpDialer` (extract it if both need it).
- Measure first: the reference QUIC build is curl.se's Windows build with ngtcp2 (ADR-0180); measure
  `--http3-only --interface 127.0.0.1 --local-port <n>` and `--interface bogus0` against a local server.

## Acceptance criteria

- [ ] The measured stdout, stderr and exit code are in Notes, and `Curl.Networking.UnitTests` pin the local end the UDP opener is asked for and each failure's exit code and message.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
