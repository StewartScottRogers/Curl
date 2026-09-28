---
id: BL-648
title: Prefix -v and --trace lines with transfer and connection IDs for --trace-ids
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-648 — Prefix -v and --trace lines with transfer and connection IDs for --trace-ids

## Goal

`--trace-ids` parses and prefixes every `-v`, `--trace` and `--trace-ascii` line with curl 8.21.0's `[<xfer>-<conn>]` marker (with its form before a connection exists), byte for byte.

## Context

- Conformance audit 2026-09-28, row 30 (Major).
- Formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TraceTransferEventWriter.cs`, `TraceTimeStamp.cs` (`--trace-time` is the precedent for a per-line prefix and its position relative to the time). IDs come from `Curl.Console` (ADR-0060: `%{xfer_id}` and `%{conn_id}`).

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -Connections 2`: `-v --trace-ids` for two URLs, `--trace-ascii - --trace-ids`, and `-v --trace-ids --trace-time`; stderr (or the trace output) copied into Notes with varying parts marked.
- [ ] Tests pin each measured output, normalised as existing `-v` tests are; `Curl.Cli.UnitTests` pin parsing.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
