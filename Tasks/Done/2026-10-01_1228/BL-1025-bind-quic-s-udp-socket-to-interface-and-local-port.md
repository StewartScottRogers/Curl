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
completed: 2026-10-01
---
# BL-1025 — Bind QUIC's UDP socket to --interface and --local-port

## Goal

An `--http3` or `--http3-only` transfer binds its UDP socket as `--interface` and `--local-port` ask, as libcurl's `bindlocal` binds every IP socket, with the same exit 45 and exit 43 failures as TCP.

## Context

- Follow-up from BL-600 (ADR-0269, Consequences): `TcpConnector` binds TCP dials through
  `LocalBindingTcpDialer`, but `ConnectMultiplexedAsync` hands addresses to `QuicDialer`, whose
  `IUdpChannelOpener` (`UdpChannelOpener`) binds its own local address and port.
- Reuse `LocalBinding` and the address choice in `LocalBindingTcpDialer` (extract it if both need it).
- Measure first: the reference QUIC build is curl.se's Windows build with ngtcp2 (ADR-0180); measure
  `--http3-only --interface 127.0.0.1 --local-port <n>` and `--interface bogus0` against a local server.

## Acceptance criteria

- [x] The measured stdout, stderr and exit code are in Notes, and `Curl.Networking.UnitTests` pin the local end the UDP opener is asked for and each failure's exit code and message.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

### Measured 2026-10-01 (curl.se 8.18.0 LibreSSL/ngtcp2, Windows; `Record-CurlExchange.ps1 -Port 47611 -UdpSink -Curl <WinGet curl.exe>`)

All `-sS --http3-only https://127.0.0.1:47611/` unless stated.

| Arguments | stdout | stderr | exit |
| --- | --- | --- | --- |
| `--connect-timeout 2 --interface 127.0.0.1 --local-port 41000-41010 -w [%{local_ip}:%{local_port}]` | `[:-1]` | `curl: (28) Connection timed out after 2005 milliseconds` (3 datagrams reached the sink) | 28 |
| same with `-v` | | `Trying 127.0.0.1:47611...`, `Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2`, `Local port: 41000`, then `SSL Trust Anchors:` | 28 |
| `--interface bogus0` | | `curl: (45) Could not bind to 'bogus0' with errno 0: The operation completed successfully.` | 45 |
| same with `-v` | | `Trying`, `Could not resolve host: 127.0.0.1`, `Could not bind to 'bogus0' ...`, `Failed to connect to 127.0.0.1 port 47611 after 2861 ms: Failed binding local connection end`; no trust anchors, no `QUIC connect to` | 45 |
| `--interface host!nosuch.invalid` | | `curl: (45) Could not bind to 'nosuch.invalid' with errno 0: The operation completed successfully.` | 45 |
| `-v --local-port 41000-41002`, all three held | | `Bind to local port 41000 failed, trying next`, `... 41001 ...`, `bind failed with errno 10048: Address already in use`, `Failed to connect to 127.0.0.1 port 47611 after 0 ms: Failed binding local connection end`; error line `curl: (45) bind failed with errno 10048: Address already in use` | 45 |
| `-v --local-port 41000-41005`, 41000-41002 held | | three `Bind ... trying next` lines, `Local port: 41003` | 28 |
| `-v --interface ::1` | | `Name '::1' family 2 resolved to '::1' family 23`, `curl: (7) Failed to connect to 127.0.0.1 port 47611 after 0 ms: Could not connect to server` | 7 |
| `--interface ifhost!<255 a>!127.0.0.1` | | `curl: (43) setopt 0x274e got bad argument` (8.18.0 refuses at setopt; 8.21.0 at connect) | 43 |
| `--http3 --interface bogus0` | | the exit 45 `Could not bind` line | 45 |
| TCP `--interface bogus0 http://...`, same 8.18.0 | | the same `Could not bind to 'bogus0'` line | 45 |

So 8.18.0's `curl: (45)` text is the version's, not QUIC's: TCP reads the same.

### Decisions (ADR-0292, decided by Claude under Stewart's delegation)

- Extracted `LocalBindingAddressChooser` from `LocalBindingTcpDialer`; `TcpConnector` puts it on `QuicDialRequest.LocalBinding`.
- `IUdpChannelOpener.OpenFrom(server, localEndPoint, localPortCount)`; `UdpChannelOpener` walks the range with `TcpDialer.BindLocalEnd`.
- `QuicDialer` binds before reporting the trust anchors (the measured order) and closes the socket when the trust anchors cannot be read.
- A bind failure moves on to the next address and ends with exit 45, 43 or 7 and the one line
  `Failed to connect to <host> port <port> after N ms: <words>`: the TCP path's (8.21.0) words in QUIC's form, not 8.18.0's `Could not bind` text.
- The `Could not bind`, `Bind to local port N failed, trying next` and `Local port: N` `-v` lines stay BL-1027's.
- `--ai-help`: no option added or changed, so unchanged. `Curl.Console` unchanged: it already passes `LocalBinding` to `TcpConnector`.
- ADR-0292 and its README row added (no `touches` needed). Tests: `Curl.Networking.UnitTests/TcpConnectorQuicTests.LocalBinding.cs`;
  `Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --http3 and --http3-only bind QUIC's UDP socket as --interface and --local-port ask, with exit 45, 43 and 7 bind failures as on TCP
