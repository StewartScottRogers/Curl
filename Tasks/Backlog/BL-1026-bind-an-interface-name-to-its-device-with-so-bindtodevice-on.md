---
id: BL-1026
title: Bind an --interface name to its device with SO_BINDTODEVICE on Linux
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-600]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-1026 — Bind an --interface name to its device with SO_BINDTODEVICE on Linux

## Goal

On Linux, `--interface <name>`, `if!<name>` and `ifhost!<name>!<host>` first try `SO_BINDTODEVICE` on the socket as libcurl's `bindlocal` does, and a success binds the socket to the device as curl does.

## Context

- Follow-up from BL-600 (ADR-0268, Consequences). libcurl 8.21.0's `bindlocal` (`lib/cf-socket.c`)
  calls `setsockopt(SO_BINDTODEVICE)` for an interface name under 255 characters; without privilege it
  fails and curl carries on as BL-600 does; with privilege (root or `CAP_NET_RAW`) a plain or `if!` name
  returns bound to the device without binding an address, and `ifhost!` goes on to bind the host.
- .NET reaches it with `Socket.SetRawSocketOption(SOL_SOCKET = 1, SO_BINDTODEVICE = 25, name + NUL)`,
  Linux only; keep the call in `TcpDialer` (thin, ADR-0083) and the decision testable, e.g. through
  `LocalBinding.DeviceName` and a seam in `LocalBindingTcpDialer`.
- Measure on Linux (WSL has curl) as root and not, with `-v`, to pin the lines and results.

## Acceptance criteria

- [ ] The measured behaviour with and without privilege is in Notes, and `Curl.Networking.UnitTests` pin the device bind asked for, under `OSCondition(OperatingSystems.Linux)` where the platform decides it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-09-29: Created.
