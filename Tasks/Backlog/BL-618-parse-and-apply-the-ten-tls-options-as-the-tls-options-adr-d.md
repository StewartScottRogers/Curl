---
id: BL-618
title: Parse the ten TLS options and apply --engine, --dump-ca-embed and what SslStream carries
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-617]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-618 — Parse the ten TLS options and apply --engine, --dump-ca-embed and what SslStream carries

## Goal

`--curves`, `--sigalgs`, `--tls-earlydata`, `--ech`, `--ssl-sessions`, `--engine`, `--dump-ca-embed`, `--tlsuser`, `--tlspassword` and `--tlsauthtype` parse with curl 8.21.0's value checks into `CommandLineOptions` and reach `TlsClientOptions`; `--engine` and `--dump-ca-embed` produce the output BL-617's ADR gives; and whatever BL-617's ADR routes through `SslStream` is applied. The hand-built routes are BL-709 to BL-712.

## Context

- Conformance audit 2026-09-28, row 18 (Major). Decision and measurements: BL-617's ADR. Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28): no option is refused where some official curl build honours it; only malformed values are refused, with curl's text.
- Parse in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; value refusals built as the parser's other refusals are (`CommandLineRefusal.cs`); options flow through `Curl.Console/TlsClientOptionsMapping.cs` to `Curl.Networking.UnitLibrary/TlsClientOptions.cs`, where BL-708's routing function reads them.

## Acceptance criteria

- [ ] `Curl.Cli.UnitTests` cover every option with valid and malformed values (malformed ones refused with the measured text and exit code).
- [ ] Tests pin `--engine list` and `--dump-ca-embed` output as BL-617's ADR gives it, and each option reaching `TlsClientOptions`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
