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

- From BL-599 (parsing): `CommandLineOptions.Interface` is an `InterfaceBinding` (`InterfaceOrHostName`,
  `InterfaceName`, `HostName`, `IsMalformed`) and `CommandLineOptions.LocalPorts` a `LocalPortRange`
  (`First`, `Last`, `Count`). A value with `IsMalformed` set must fail the transfer before connecting with
  exit 43 and `curl: (43) setopt 0x274e got bad argument` (curl 8.21.0, Windows, measured). An `ifhost!`
  interface part longer than 254 characters fails at connect instead, with exit 43 and
  `curl: (43) Failed to connect to <host>:<port> after 0 ms: A libcurl function was given a bad argument`.
  Unbindable names measured on Windows: `nosuchif`, `if!nosuch`, ` `, `IF!`, `ifhost!!h` all give exit 45
  `curl: (45) Failed to connect to 127.0.0.1:47599 after <n> ms: Failed binding local connection end`.

## Log

- 2026-09-28: Created.
