---
id: BL-647
title: Parse --tcp-fastopen and --mptcp and act as the platform's curl does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-647 — Parse --tcp-fastopen and --mptcp and act as the platform's curl does

## Goal

`--tcp-fastopen` and `--mptcp` parse and produce what the platform's curl 8.21.0 build produces: TCP Fast Open and Multipath TCP sockets where it uses them and the BCL can open them, and the build's warning or refusal where it does not.

## Context

- Conformance audit 2026-09-28, row 29 (Major, S; filed Low as rarely used).
- `SocketOptionName.FastOpen` exists in the BCL (Windows); Multipath TCP is Linux-only (`IPPROTO_MPTCP` at socket creation, which the BCL's `Socket` constructor accepts as a raw protocol number). Measure first; the Windows reference may refuse `--mptcp`.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: each option against a loopback URL with `-v`, on Windows and on Linux or macOS; stderr and exit code copied into Notes.
- [ ] Tests pin parsing and each platform's behaviour through the dialer seam, under `OSCondition`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
