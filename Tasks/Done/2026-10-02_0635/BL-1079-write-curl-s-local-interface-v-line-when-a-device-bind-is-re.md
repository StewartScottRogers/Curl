---
id: BL-1079
title: Write curl's Local Interface -v line when a device bind is refused and the interface address is bound
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-1027]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-10-01
completed: 2026-10-02
---
# BL-1079 — Write curl's Local Interface -v line when a device bind is refused and the interface address is bound

## Goal

`curl -v --interface <name>` on Linux, when the `SO_BINDTODEVICE` bind is refused but the interface is found, writes libcurl's `Local Interface <name> is ip <address> using address family <n>` line before `Local port: N`.

## Context

- Follow-up from BL-1027 (ADR-0295, Decision 4). libcurl 8.21.0's `bindlocal` (`lib/cf-socket.c`) writes
  `infof(data, "Local Interface %s is ip %s using address family %i", iface, host, af)` when `Curl_if2ip`
  finds the interface's address (`IF2IP_FOUND`). On Linux 5.7 and later an unprivileged device bind succeeds,
  so the line shows only where `SO_BINDTODEVICE` is refused - an older kernel, a container without the
  capability, or macOS, which has no `SO_BINDTODEVICE` (it may use `IP_BOUND_IF`; check the source).
- Code: `LocalBindingAddressChooser.InterfaceAddress` (where the address is found), `LocalBindLines`.
- Measure first: on macOS (CI runner or a Mac) `curl -v --interface lo0 http://127.0.0.1:<port>/`, or on
  Linux in a container where `setsockopt(SO_BINDTODEVICE)` fails with `EPERM`.

## Acceptance criteria

- [x] The line is measured with real curl on a platform where it appears, and pinned in `Curl.Networking.UnitTests` under `OSCondition` for that platform.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

- Measured with curl 8.22.0 (curlimages/curl) in Docker with a seccomp profile refusing `setsockopt(SOL_SOCKET, SO_BINDTODEVICE)` with EPERM: a plain name writes `Local Interface lo is ip 127.0.0.1 using address family 2` then `Name '127.0.0.1' family 2 resolved to '127.0.0.1' family 2`; `if!lo` writes the first line alone; `ifhost!` neither; IPv6 is family 10. Full table in ADR-0353.
- Seccomp chosen over an old kernel or a capability-less container: since Linux 5.7 no capability is needed, so a filter on the exact call is the only way to get the refused bind on this machine.
- Implemented in `LocalBindingAddressChooser.ReportInterfaceFound` with `LocalBindLines.LocalInterface`; pinned in `TcpConnectorTests.LocalInterfaceLine.cs` (IPv6 under `OSCondition(Linux)`) and `LocalBindLinesTests`. Measure-CodeQuality: 0 failing members for Curl.Networking.UnitLibrary.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. -v --interface writes curl's Local Interface line (and a plain name's Name line) when the device bind is refused and the interface is found
