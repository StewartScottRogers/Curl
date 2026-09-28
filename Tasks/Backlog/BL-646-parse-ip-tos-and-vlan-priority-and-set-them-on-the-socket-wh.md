---
id: BL-646
title: Parse --ip-tos and --vlan-priority and set them on the socket where the platform allows
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-646 — Parse --ip-tos and --vlan-priority and set them on the socket where the platform allows

## Goal

`--ip-tos <string|number>` (named values such as `CS1`, `AF11`, `LE`, or 0-255) and `--vlan-priority <0-7>` parse with curl 8.21.0's checks and, where the platform's build applies them, set `IP_TOS`/`IPV6_TCLASS` and `SO_PRIORITY` on the socket; where the build ignores or refuses them, Curl does the same.

## Context

- Conformance audit 2026-09-28, row 29 (Major, S; filed Low as a rarely used pair).
- `SocketOptionName.TypeOfService` is in the BCL; `SO_PRIORITY` is Linux-only and needs `Socket.SetRawSocketOption`. Measure the Windows reference build's reaction first.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: `--ip-tos CS1`, `--ip-tos 300`, `--ip-tos bogus`, `--vlan-priority 3`, `--vlan-priority 9`, on Windows and on Linux or macOS; stderr and exit code copied into Notes.
- [ ] Tests pin parsing, refusals and the socket options requested through the dialer seam, per platform with `OSCondition`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
