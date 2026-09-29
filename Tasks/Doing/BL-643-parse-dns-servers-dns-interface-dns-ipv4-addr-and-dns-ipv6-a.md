---
id: BL-643
title: Parse --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr on every platform
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-643 — Parse --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr on every platform

## Goal

The four c-ares options parse into `CommandLineOptions` on every platform with the value checks a c-ares build of curl 8.21.0 applies (server list syntax, interface name, IPv4 and IPv6 address), instead of `is unknown`; resolving through them is BL-694.

## Context

- Conformance audit 2026-09-28, row 28 (Minor). Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): curl's c-ares builds support these options, so Curl supports them on every platform, with a hand-built DNS client (BL-694); a build without c-ares refusing them is recorded in Notes for information only.
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`; option texts in `CurlManual.txt`. Malformed values are refused in `CommandLineRefusal.cs` with the text a c-ares build prints.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` and a c-ares build of curl (for example a Linux distribution build that lists `AsynchDNS` with c-ares in `curl -V`): each option with a valid and a malformed value against a loopback URL; stdout, stderr, exit code copied into Notes; the reference builds' refusals, if any, recorded too.
- [ ] `Curl.Cli.UnitTests` pin each option parsing and each malformed-value refusal on every platform (no `OSCondition` refusal).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
