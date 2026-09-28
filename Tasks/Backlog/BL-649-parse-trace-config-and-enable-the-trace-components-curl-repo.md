---
id: BL-649
title: Parse --trace-config and enable the trace components Curl reports
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-648]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-28
completed:
---
# BL-649 — Parse --trace-config and enable the trace components Curl reports

## Goal

`--trace-config <list>` parses curl 8.21.0's comma list (`ids`, `time`, `all`, `-name` to disable, protocol and component names) and turns on the parts Curl can report (`ids` as `--trace-ids`, `time` as `--trace-time`, `all` as both plus verbose output), with the output the reference build gives for each.

## Context

- Conformance audit 2026-09-28, row 30 (Major; filed Low because the component logs are libcurl internals). Depends on BL-648 for `ids`.
- Component names (`tls`, `http/2`, `dns`, and so on) make libcurl log internal lines Curl does not have; measure what the reference build prints for `--trace-config tls` and `all`, and record in an ADR marked "Decided by Claude under Stewart's delegation" which component lines Curl reproduces and which it cannot.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1` (and `-Tls -k`): `--trace-config ids,time`, `--trace-config all`, `--trace-config tls`, `--trace-config bogus`; stderr copied into Notes.
- [ ] `Curl.Cli.UnitTests` pin parsing; tests pin `ids`, `time` and the unknown-name behaviour as measured.
- [ ] The ADR exists in `Documentation/Planning/Decisions` (number checked unused) and is indexed in its `README.md`.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
