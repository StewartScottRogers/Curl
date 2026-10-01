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
completed:
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

- [ ] The line is measured with real curl on a platform where it appears, and pinned in `Curl.Networking.UnitTests` under `OSCondition` for that platform.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Networking.UnitLibrary`.

## Notes

## Log

- 2026-10-01: Created.
