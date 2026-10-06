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
completed: 2026-10-01
---
# BL-1026 — Bind an --interface name to its device with SO_BINDTODEVICE on Linux

## Goal

On Linux, `--interface <name>`, `if!<name>` and `ifhost!<name>!<host>` first try `SO_BINDTODEVICE` on the socket as libcurl's `bindlocal` does, and a success binds the socket to the device as curl does.

## Context

- Follow-up from BL-600 (ADR-0269, Consequences). libcurl 8.21.0's `bindlocal` (`lib/cf-socket.c`)
  calls `setsockopt(SO_BINDTODEVICE)` for an interface name under 255 characters; without privilege it
  fails and curl carries on as BL-600 does; with privilege (root or `CAP_NET_RAW`) a plain or `if!` name
  returns bound to the device without binding an address, and `ifhost!` goes on to bind the host.
- .NET reaches it with `Socket.SetRawSocketOption(SOL_SOCKET = 1, SO_BINDTODEVICE = 25, name + NUL)`,
  Linux only; keep the call in `TcpDialer` (thin, ADR-0083) and the decision testable, e.g. through
  `LocalBinding.DeviceName` and a seam in `LocalBindingTcpDialer`.
- Measure on Linux (WSL has curl) as root and not, with `-v`, to pin the lines and results.

## Acceptance criteria

- [x] The measured behaviour with and without privilege is in Notes, and `Curl.Networking.UnitTests` pin the device bind asked for, under `OSCondition(OperatingSystems.Linux)` where the platform decides it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- Measured 2026-10-01: curl 8.18.0 (OpenSSL, Ubuntu under WSL, kernel 6.x), `curl -v <opts> http://127.0.0.1:1/`,
  as uid 1000 and as root - identical answers, because since Linux 5.7 an unprivileged process may
  `SO_BINDTODEVICE` a socket not yet bound to a device:
  - `--interface lo`, `if!lo`: `socket successfully bound to interface 'lo'`, then connect from 127.0.0.1, exit 7.
  - `--interface lo --local-port 40000-40010`: bound to the device, connects from an ephemeral port (47702):
    a device bind skips the port range too.
  - `ifhost!lo!127.0.0.1`, `ifhost!bogus0!127.0.0.1`: no device line; `Name '127.0.0.1' family 2 resolved to
    '127.0.0.1' family 2`, `Local port: 0`, exit 7.
  - `host!127.0.0.1`: no device step, same lines as `ifhost!`.
  - `if!bogus0`: `Could not bind to interface 'bogus0' with errno 19: No such device`, exit 45.
  - `--interface eth0` to 127.0.0.1: bound to `eth0`, connect from 172.26.x times out, exit 28.
- Design (ADR-0293): `ITcpDialer.DialFromDeviceAsync` (default method: no device, so Curl.Console's dialers
  are untouched) takes the device, whether the address follows (`ifhost!` only), and a callback choosing the
  local end only when needed; `LocalBindingTcpDialer` decides, `TcpDialer.TryBindToDevice` calls
  `SetRawSocketOption(1, 25, name + NUL)` on Linux only (excluded per ADR-0083, pinned by two Linux tests).
- No .NET SDK in WSL, so the two `OSCondition(Linux)` tests first run in CI's Linux job.
- Follow-ups filed: BL-1076 (the `-v` line `socket successfully bound to interface`), BL-1077 (the same
  device bind on QUIC's UDP socket).

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. On Linux --interface names bind their device with SO_BINDTODEVICE first, as curl does
