---
id: BL-601
title: Parse --proxy-basic, --proxy-digest, --proxy-ntlm, --proxy-negotiate and --proxy-anyauth
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-601 — Parse --proxy-basic, --proxy-digest, --proxy-ntlm, --proxy-negotiate and --proxy-anyauth

## Goal

The five proxy authentication switches parse into a proxy auth-scheme set on `CommandLineOptions`, combined as curl 8.21.0 combines them, mirroring how `--basic`, `--digest`, `--ntlm`, `--negotiate` and `--anyauth` already set the server scheme set.

## Context

- Conformance audit 2026-09-28, row 14 (Major). Using them is BL-602 to BL-604.
- The server-side switches are rows in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; ADR-0026 records how they parse. `HttpAuthSchemes` is in Abstractions.

## Acceptance criteria

- [ ] Every switch, and combinations (`--proxy-digest --proxy-basic`, `--proxy-anyauth --proxy-basic`), are covered by `Curl.Cli.UnitTests` with the set curl keeps (measure any doubtful combination with `Record-CurlExchange.ps1` as a proxy and record it in Notes).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
