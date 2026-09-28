---
id: BL-618
title: Parse and apply the ten TLS options as the TLS-options ADR decides
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-617]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-618 — Parse and apply the ten TLS options as the TLS-options ADR decides

## Goal

`--curves`, `--sigalgs`, `--tls-earlydata`, `--ech`, `--ssl-sessions`, `--engine`, `--dump-ca-embed`, `--tlsuser`, `--tlspassword` and `--tlsauthtype` produce, on each platform, exactly the output and exit code BL-617's ADR table gives.

## Context

- Conformance audit 2026-09-28, row 18 (Major). Decision and measurements: BL-617's ADR.
- Parse in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; refusals built as the parser's other refusals are (`CommandLineRefusal.cs`) or at transfer time as the ADR says; anything honoured goes through `Curl.Console/TlsClientOptionsMapping.cs` to `Curl.Networking.UnitLibrary`.

## Acceptance criteria

- [ ] One test per option per platform (`OSCondition`) pins the ADR's stdout, stderr and exit code.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
