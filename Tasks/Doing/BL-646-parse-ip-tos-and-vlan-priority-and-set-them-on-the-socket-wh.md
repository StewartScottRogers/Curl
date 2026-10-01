---
id: BL-646
title: Parse --ip-tos and --vlan-priority and set them on the socket on every OS that has the option
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-646 — Parse --ip-tos and --vlan-priority and set them on the socket on every OS that has the option

## Goal

`--ip-tos <string|number>` (named values such as `CS1`, `AF11`, `LE`, or 0-255) and `--vlan-priority <0-7>` parse with curl 8.21.0's checks on every platform and set `IP_TOS`/`IPV6_TCLASS` and `SO_PRIORITY` on the socket on every operating system that has those socket options, whatever the platform's usual curl build does; only where the operating system itself has no such option does Curl do what curl does on that OS.

## Context

- Conformance audit 2026-09-28, row 29 (Major, S; filed Low as a rarely used pair). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): if any official curl build supports a feature, Curl supports it on every platform; the only limit is what the operating system can do at all, never what the BCL wraps (use `Socket.SetRawSocketOption` with the OS's level and name where the BCL has no enum value).
- `SocketOptionName.TypeOfService` is in the BCL; `SO_PRIORITY` is a Linux socket option set through `Socket.SetRawSocketOption`. curl's `src/tool_operate.c`/`lib/cf-socket.c` at tag `curl-8_21_0` show which OSes curl applies each on; record that in Notes.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--ip-tos CS1`, `--ip-tos 300`, `--ip-tos bogus`, `--vlan-priority 3`, `--vlan-priority 9`, on Windows and on Linux or macOS; stderr and exit code copied into Notes.
- [ ] Tests pin parsing, value refusals and the socket options requested through the dialer seam; `OSCondition` separates only operating systems that lack a socket option altogether, never a platform whose curl build merely ignores it.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
