---
id: BL-647
title: Parse --tcp-fastopen and --mptcp and open those sockets on every OS that supports them
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-647 — Parse --tcp-fastopen and --mptcp and open those sockets on every OS that supports them

## Goal

`--tcp-fastopen` and `--mptcp` parse on every platform and open TCP Fast Open and Multipath TCP connections on every operating system that supports them (TCP Fast Open on Windows, Linux and macOS; MPTCP on Linux, and on macOS if its sockets API allows it), whatever the platform's usual curl build does; only where the operating system itself cannot does Curl do what curl does on that OS, with curl's text.

## Context

- Conformance audit 2026-09-28, row 29 (Major, S; filed Low as rarely used). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): if any official curl build supports a feature, Curl supports it on every platform; the limit is the operating system, never what the BCL wraps (use raw socket options or raw protocol numbers where the BCL has no enum value).
- `SocketOptionName.FastOpen` exists in the BCL (Windows); Linux uses `TCP_FASTOPEN_CONNECT` or `MSG_FASTOPEN`, macOS `connectx` semantics, per curl's `lib/cf-socket.c` at tag `curl-8_21_0` (record the route per OS in Notes); Multipath TCP is `IPPROTO_MPTCP` (262) at socket creation on Linux, which the BCL's `Socket` constructor accepts as a raw protocol number, falling back to TCP as curl does when the kernel refuses it. Measure first on each OS.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: each option against a loopback URL with `-v`, on Windows and on Linux or macOS; stderr and exit code copied into Notes.
- [ ] Tests pin parsing and the socket requested through the dialer seam per operating system; `OSCondition` separates only operating systems that lack the facility altogether.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
