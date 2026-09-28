---
id: BL-600
title: Bind the local address and port --interface and --local-port name, failing with exit 45
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-599]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-600 — Bind the local address and port --interface and --local-port name, failing with exit 45

## Goal

The TCP connector binds the socket to the address `--interface` names (an interface name, a host name or an address, per its prefix) and to the first free port in the `--local-port` range before connecting, and a name or port that cannot be bound fails with exit 45 (`CURLE_INTERFACE_FAILED`) and curl 8.21.0's message.

## Context

- Conformance audit 2026-09-28, row 13 (Major). Parsing is BL-599.
- Code: `Curl.Networking.UnitLibrary/TcpConnector.cs`, `ITcpDialer.cs`/`TcpDialer.cs` (ADR-0083: the adapter is thin, so unit tests go through `ITcpDialer`), `SystemNetworkInterfaceLookup.cs` and `INetworkInterfaceLookup` (already used for FTP active mode, ADR-0110), and the connection pool key (a bound address must not share a connection with an unbound one).
- Interface names differ by platform; measure on Windows and on Linux or macOS.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--interface 127.0.0.1`, `--interface bogus0`, `--interface if!lo` (Linux) or the loopback adapter name (Windows), `--local-port 40000-40010 -w '%{local_port}'`, and a port range already all in use; stdout, stderr and exit code copied into Notes.
- [ ] `Curl.Networking.UnitTests` pin the bind requested through the dialer seam and exit 45 with the measured message for each failure; each platform's interface-name answer is pinned under `OSCondition`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary` and `Curl.Console`.

## Notes

## Log

- 2026-09-28: Created.
