---
id: BL-643
title: Parse --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr as the reference build does
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-643 — Parse --dns-servers, --dns-interface, --dns-ipv4-addr and --dns-ipv6-addr as the reference build does

## Goal

The four c-ares options produce exactly what the platform's curl 8.21.0 build produces: most likely a refusal (these need a c-ares build, which the Windows reference is probably not), so Curl prints that refusal instead of `is unknown`.

## Context

- Conformance audit 2026-09-28, row 28 (Minor; "likely refused by reference build, measure first").
- Rows in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`. Refusals are built in `CommandLineRefusal.cs` or at transfer time, wherever the measurement says curl refuses (before or after connecting).
- If a platform's usual build does support them, Curl cannot honour them without a DNS client: record that in Notes and pin the reference refusal only for the platforms that refuse, leaving the others to a follow-up task that you file.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: each option with a plausible value and a loopback URL, on Windows and on Linux or macOS; stdout, stderr, exit code and whether a connection was made copied into Notes.
- [ ] Tests pin each platform's measured output under `OSCondition`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
