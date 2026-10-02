---
id: BL-654
title: Write the --libcurl setopt lines for the TLS, proxy, authentication and protocol options
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-653]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-654 — Write the --libcurl setopt lines for the TLS, proxy, authentication and protocol options

## Goal

The `--libcurl` generator covers every remaining option `CommandLineOptionTable` parses (TLS, proxy, authentication, FTP, TFTP, telnet, MQTT, retry and rate options, and any added since), writing curl 8.21.0's lines for each, so no parsed option is silently missing from the generated source.

## Context

- Conformance audit 2026-09-28, row 30. Builds on BL-653.
- Add a test that enumerates `CommandLineOptionTable` and fails for any option with no generator entry (or an explicit "curl writes nothing for this" entry), so options added later cannot be forgotten.
- Measure each option's lines with `Record-CurlExchange.ps1 --libcurl -` as in BL-653.

## Acceptance criteria

- [ ] Measured first; output copied into Notes.
- [ ] `Curl.Cli.UnitTests` reproduce each measured output byte for byte, and the enumeration test passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-10-01: Backlog -> Doing.
