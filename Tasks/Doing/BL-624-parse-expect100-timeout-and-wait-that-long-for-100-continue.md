---
id: BL-624
title: Parse --expect100-timeout and wait that long for 100 Continue
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-624 — Parse --expect100-timeout and wait that long for 100 Continue

## Goal

`--expect100-timeout <seconds>` (decimal allowed) replaces the one-second wait for `100 Continue` before a request body is sent anyway, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 21 (Major, S).
- The current one-second wait is in `Curl.Protocol.Http.UnitLibrary` (ADR-0036 frames request bodies; `Record-CurlExchange.ps1 -RespondAfterBodyBytes` documents curl's one-second wait). Parse with the other decimal-seconds options (`--connect-timeout`) in `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs`; the value reaches the handler through `HttpRequestOptions` via `Curl.Console/HttpRequestOptionsMapping.cs`.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1 -ResponseDelayMilliseconds`: a large `-d @file` (curl sends `Expect: 100-continue` above its threshold) with `--expect100-timeout 0.2` and `3`, timing when the body arrives; and `--expect100-timeout abc`; request bytes, timings and exit codes copied into Notes.
- [ ] `Curl.Protocol.Http.UnitTests` on a fake `TimeProvider` pin when the body is sent for each timeout; `Curl.Cli.UnitTests` pin parsing and the refusal.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
