---
id: BL-646
title: Parse --ip-tos and --vlan-priority and set them on the socket on every OS that has the option
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-10-01
---
# BL-646 — Parse --ip-tos and --vlan-priority and set them on the socket on every OS that has the option

## Goal

`--ip-tos <string|number>` (named values such as `CS1`, `AF11`, `LE`, or 0-255) and `--vlan-priority <0-7>` parse with curl 8.21.0's checks on every platform and set `IP_TOS`/`IPV6_TCLASS` and `SO_PRIORITY` on the socket on every operating system that has those socket options, whatever the platform's usual curl build does; only where the operating system itself has no such option does Curl do what curl does on that OS.

## Context

- Conformance audit 2026-09-28, row 29 (Major, S; filed Low as a rarely used pair). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): if any official curl build supports a feature, Curl supports it on every platform; the only limit is what the operating system can do at all, never what the BCL wraps (use `Socket.SetRawSocketOption` with the OS's level and name where the BCL has no enum value).
- `SocketOptionName.TypeOfService` is in the BCL; `SO_PRIORITY` is a Linux socket option set through `Socket.SetRawSocketOption`. curl's `src/tool_operate.c`/`lib/cf-socket.c` at tag `curl-8_21_0` show which OSes curl applies each on; record that in Notes.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1`: `--ip-tos CS1`, `--ip-tos 300`, `--ip-tos bogus`, `--vlan-priority 3`, `--vlan-priority 9`, on Windows and on Linux or macOS; stderr and exit code copied into Notes.
- [x] Tests pin parsing, value refusals and the socket options requested through the dialer seam; `OSCondition` separates only operating systems that lack a socket option altogether, never a platform whose curl build merely ignores it.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- **Measured 2026-10-01**, curl 8.21.0 (Schannel, Windows) against `http://127.0.0.1:1/` (nothing
  listening), and Ubuntu's curl 8.18.0 (OpenSSL, WSL) for the exit codes. The refusals happen while
  the command line is parsed, before any exchange, so curl was run directly rather than through
  `Record-CurlExchange.ps1`'s loopback server; stderr and exit code are all there is to record.
  - `--ip-tos CS1`, `--ip-tos LE`, `--ip-tos 255`, `--vlan-priority 3`, `--vlan-priority -0`,
    `--vlan-priority 07`: accepted; the transfer goes on to exit 7
    (`curl: (7) Failed to connect to 127.0.0.1:1 after N ms: Could not connect to server`) on both.
  - `--ip-tos 300`: `curl: option --ip-tos: too large number`, then
    `curl: try 'curl --help' or 'curl --manual' for more information`, exit 2 (Linux exit 2).
  - `--ip-tos bogus`, `--ip-tos cs1` (names are case-sensitive):
    `curl: option --ip-tos: expected a proper numerical parameter`, exit 2 (Linux exit 2).
  - `--ip-tos -1`, `--vlan-priority -1`: `... expected a positive numerical parameter`, exit 2.
  - `--vlan-priority 9`: `curl: option --vlan-priority: too large number`, exit 2 (Linux exit 2).
  - `--vlan-priority x`, `+3`, `3x`, empty, `99999999999999999999`:
    `... expected a proper numerical parameter`, exit 2.
  - All 30 names of `tos_entries` are accepted (AF11-AF43, CE, CS0-CS7, ECT0, ECT1, EF, LE, LOWCOST,
    LOWDELAY, MINCOST, RELIABILITY, THROUGHPUT); `NOTCT`, `CS8`, `VA` are refused. `--libcurl` shows
    neither option and the WSL image has no `strace`, so the byte each name stands for is taken from
    curl's `src/tool_getparam.c` table (DSCP shifted left 2, ECN, RFC 1349).
  - Neither option writes a `-v` line (Linux `-v --ip-tos CS1 --vlan-priority 3` shows only the
    usual connect lines).
- **Where curl applies them** (`lib/cf-socket.c`, curl-8_21_0): `IP_TOS` for IPv4 and `IPV6_TCLASS`
  for IPv6 where defined, `SO_PRIORITY` where defined; 0 is never passed to libcurl, and a refused
  `setsockopt` is only logged. Of Curl's platforms only Linux has `SO_PRIORITY`; Windows, macOS and
  FreeBSD have `IP_TOS` and `IPV6_TCLASS`, so Curl sets those there too (ADR-0316).
- **What was done:** `CommandLineOptions.IpTypeOfService` and `VlanPriority` with
  `CommandLineOptionTable` rows (`ParseAtMost`: `ParseNonNegative`, then "too large number");
  `TcpSocketOptions.TypeOfService` and `VlanPriority`; `QualityOfServiceSocketOptions.For` lists the
  raw options per `SocketPlatform` and address family, `TrySet` swallows a refusal, and
  `TcpDialer.ApplySocketOptions` applies them; `CurlComposition.CreateTransports` passes both on.
- **Touches widened** to `Curl.Console` and `Curl.Console.UnitTests`: the dialer's options are built
  in `CurlComposition.CreateTransports`, so without it the options would parse but never reach the
  socket. No task in Doing on `origin/work/dark-factory` named either.
- `SocketPlatform` is public, in its own file, so the data-driven tests can take it as a parameter.
- `OSCondition` marks only the read-back tests that need a real kernel (Linux for `SO_PRIORITY` and
  `IP_TOS`, Linux and macOS for `IPV6_TCLASS`); the choice of numbers is pinned for every platform
  on every platform.
- `--ai-help` needed no edit: it drops its "Not supported by this build yet" line once
  `CommandLineOptionTable` has the option's row, which both now have.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green; `Measure-CodeQuality.ps1
  -Library` reports 0 failing members for `Curl.Networking.UnitLibrary` and `Curl.Cli.UnitLibrary`.

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
- 2026-10-01: Doing -> Done. --ip-tos and --vlan-priority parse as curl 8.21.0 does and set IP_TOS/IPV6_TCLASS and SO_PRIORITY wherever the OS has them
