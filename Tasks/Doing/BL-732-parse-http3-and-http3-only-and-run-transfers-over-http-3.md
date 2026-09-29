---
id: BL-732
title: Parse --http3 and --http3-only and run transfers over HTTP/3
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-731, BL-728]
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-732 — Parse --http3 and --http3-only and run transfers over HTTP/3

## Goal

`--http3` and `--http3-only` are accepted instead of refused (ADR-0017's exit 2 goes away), and `Curl.Console` runs an `https://` transfer over HTTP/3: `--http3` races QUIC against TCP and falls back to HTTP/2 or HTTP/1.1 as BL-718's ADR records curl doing, `--http3-only` uses QUIC alone and fails with the measured exit when it cannot, on every platform.

## Context

- Standing rule (root `CLAUDE.md`, "Decisions", 2026-09-28). Refusal today: the `http3` and `http3-only` rows in `Curl.Cli.UnitLibrary` (ADR-0017, superseded by BL-718's ADR). Composition: `Curl.Console/CurlTransports.cs`, `CurlComposition.cs`; version choice via the new `HttpVersionPreference` values (BL-721). QUIC connector: BL-728; handler path: BL-731.
- `--http3` with an `http://` URL, and combinations with `--http1.1`/`--http2` (last wins or not), follow curl's measured behaviour from BL-718.

## Acceptance criteria

- [ ] `Curl.Cli.UnitTests` show both options parsing into the version preference, with the interplay rules as measured.
- [ ] `Curl.Console.UnitTests` with fake connectors show an HTTP/3 transfer end to end, `--http3` falling back when the QUIC dial fails, `--http3-only` failing with the measured exit and message, on every platform (no `OSCondition` refusal).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
