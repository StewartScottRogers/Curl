---
id: BL-1076
title: Write curl's -v line 'socket successfully bound to interface' after a device bind
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-1026]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-01
---
# BL-1076 — Write curl's -v line 'socket successfully bound to interface' after a device bind

## Goal

On Linux, a plain `--interface <name>` or `if!<name>` whose `SO_BINDTODEVICE` succeeds writes the `-v` line `socket successfully bound to interface '<name>'` right after `Trying <ip>:<port>...`, as curl does.

## Context

- Follow-up from BL-1026 (ADR-0293, Consequences). `TcpDialer.DialFromDeviceAsync` binds the device but
  `ITcpDialer` has no way to tell `TcpConnector` it did, and the line must appear even when the connect
  then fails (measured: curl 8.18.0 on WSL, `--interface lo http://127.0.0.1:1/` prints it before
  `connect to 127.0.0.1 port 1 from 127.0.0.1 port N failed: Connection refused`).
- `ifhost!` writes no such line even when the device bind succeeds (measured).
- libcurl 8.21.0 writes it from `bindlocal` in `lib/cf-socket.c` through `infof`.

## Acceptance criteria

- [x] A `Curl.Networking.UnitTests` test pins the line, after the `Trying` line, for a plain and an `if!` name whose device bind succeeds, and its absence for `ifhost!` and for a refused device bind.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- BL-1027 had already written the line inside `TcpDialer.DialDeviceBoundAsync`, through the `ITransferEvents` `DialFromDeviceAsync` takes, so `ITcpDialer` needed no new member; but that code is excluded from coverage (ADR-0083) and nothing outside the Linux Integration run pinned it.
- Moved the device-or-address decision and the line into the covered `TcpDialer.BindDeviceOrLocalEndAsync`, which takes the device bind and address bind as delegates; `DialDeviceBoundAsync` passes the real socket calls (lambdas marked excluded like their method), and `FakeDeviceBindingTcpDialer` now runs the same method, so the connector tests exercise production logic.
- Tests: `TcpDialerTests.BindDeviceOrLocalEndAsync_ForEachDeviceBindOutcome_ReportsCurlsLineOrBindsTheAddress` (four outcomes) and `TcpConnectorTests.DeviceBindLine.cs` (line after `Trying` for plain and `if!`, still there when the connect is refused, absent for `ifhost!` and a refused device bind).
- Measure-CodeQuality.ps1 -Library Curl.Networking.UnitLibrary: 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. -v writes 'socket successfully bound to interface' after Trying for a plain or if! name whose device bind succeeds, pinned by unit tests
