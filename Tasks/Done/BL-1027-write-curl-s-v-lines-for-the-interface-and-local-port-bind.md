---
id: BL-1027
title: Write curl's -v lines for the --interface and --local-port bind
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-600]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-29
completed: 2026-10-01
---
# BL-1027 — Write curl's -v lines for the --interface and --local-port bind

## Goal

`curl -v` with `--interface` or `--local-port` writes the bind lines curl 8.21.0 writes between `Trying` and the connect outcome.

## Context

- Follow-up from BL-600 (ADR-0269, Consequences). BL-600 binds and fails with the right exit codes and
  the `connect to ... from  port 0 failed:` line, but not these lines. Code: `LocalBindingTcpDialer`,
  `TcpDialer.BindLocalEnd`, `AddressFamilyRace` in `Curl.Networking.UnitLibrary`; the dialer has no
  `ITransferEvents` today, so the lines need a way out (return them with the dial, or pass the events).
- Measured on Windows 2026-09-29 (BL-600 Notes), curl 8.21.0 Schannel:
  - `--interface 127.0.0.1 --local-port 40010-40012`: `Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2`, `Local port: 40010`.
  - `--interface 127.0.0.1` to `localhost`: for `::1` `Name '127.0.0.1' family 23 resolved to '127.0.0.1' family 2`; bound with no port: `Local port: 0`.
  - `--local-port 40020` alone: `Local port: 40020`.
  - `--local-port 40000-40002` all busy: `Bind to local port 40000 failed, trying next`, `Bind to local port 40001 failed, trying next`, `bind failed with errno 10048: Address already in use`.
  - `--interface bogus0`: `Could not resolve host: bogus0`, `Could not bind to 'bogus0' with errno 0: No error`.
  - `--interface if!Ethernet`: `Could not bind to interface 'Ethernet' with errno 0: No error`.
- Family numbers and errno text differ by platform (Linux AF_INET6 is 10, glibc's `strerror`); measure on Linux too.

## Acceptance criteria

- [x] Each line above is measured again with `Record-CurlExchange.ps1` and pinned in `Curl.Networking.UnitTests` for its case; platform-specific text is pinned under `OSCondition`.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

### Measured 2026-10-01

Windows, curl 8.21.0 Schannel, `Record-CurlExchange.ps1 -Port 47601 -CurlArgs -sS,-v,...` (every line from the Context
reproduced exactly), plus:
- `--interface ::1` to `127.0.0.1`: `Name '::1' family 2 resolved to '::1' family 23`, then the `from  port 0 failed` line, exit 7.
- `--interface host!localhost` to `127.0.0.1`: `Name 'localhost' family 2 resolved to '::1' family 23`, exit 7.
- `--interface ifhost!Ethernet!127.0.0.1`: `Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2`, `Local port: 0`.
- `--local-port 40000-40005` with 40000-40002 busy: three `trying next` lines, then `Local port: 40003`.

Linux, curl 8.18.0 OpenSSL under WSL Ubuntu, against a closed port (the bind lines come before the connect either way):
- `Name '127.0.0.1' family 10 resolved to '127.0.0.1' family 2` (AF_INET6 is 10), `Local port: 40010`, `Local port: 0`, `Local port: 40020`.
- `--local-port 40000-40001` both busy (held by `nc -l`): `Bind to local port 40000 failed, trying next`,
  `bind failed with errno 98: Address already in use`.
- `bogus0` and `host!nosuch.invalid`: `Could not resolve host: 127.0.0.1` (8.18.0 names the URL's host; 8.21.0 the bind host),
  `Could not bind to 'bogus0' with errno 22: Invalid argument`.
- `if!bogus0`: `Could not bind to interface 'bogus0' with errno 19: No such device`.
- `lo`, `if!lo`: `socket successfully bound to interface 'lo'`; `ifhost!lo!127.0.0.1`: no such line, then `Name ...`, `Local port: 0`.
- 8.18.0 on Linux writes no `connect to ... from  port 0 failed` line after a failed bind and ends with the failf text;
  left as BL-600/ADR-0269 decided (8.21.0 is the reference).

### Decisions (ADR-0294, decided by Claude under Stewart's delegation)

- Seam: `TcpConnector` builds each race's `LocalBindingTcpDialer` with `target.Events`; it passes them to
  `LocalBindingAddressChooser.ChooseAsync` and to the inner dialer through a new `ITcpDialer.DialFromAsync` overload
  taking `ITransferEvents` (default: the old overload, no lines, so `Curl.Console`'s dialers are untouched) and a new
  `events` parameter on `DialFromDeviceAsync`. `TcpDialer.BindLocalEnd` writes the port lines. Texts in `LocalBindLines`.
- `bind failed` errno is `SocketException.NativeErrorCode`; the words are `ConnectFailureReason`'s, which now has
  curl's `Address already in use` for WSAEADDRINUSE. macOS (unmeasurable here) gets AF_INET6 30 and the Linux errno texts.
- QUIC's bind (ADR-0292) writes no line yet (`NoTransferEvents`), and libcurl's `Local Interface ... is ip ...` line could
  not be produced to measure: filed as BL-1078 and BL-1079.
- `--ai-help`: no option added or changed, so its text is unchanged.

## Log

- 2026-09-29: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. curl -v with --interface/--local-port writes curl's bind lines (Name resolved, Local port, trying next, bind failed, Could not bind) on Windows and Linux
