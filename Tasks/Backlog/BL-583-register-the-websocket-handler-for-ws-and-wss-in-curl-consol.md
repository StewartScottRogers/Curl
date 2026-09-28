---
id: BL-583
title: Register the WebSocket handler for ws and wss in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-582]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-583 — Register the WebSocket handler for ws and wss in Curl.Console

## Goal

`curl ws://...` and `curl wss://...` run end to end through `Curl.Console` with the WebSocket handler, the connector (and proxies, if BL-579's ADR applies them), and the HTTP options the ADR names, producing the measured request bytes, output and exit codes.

## Context

- Conformance audit 2026-09-28, row 36. Handler: BL-580 to BL-582.
- Register in `Curl.Console/CurlComposition.cs`, map options in `TransferContextFactory.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `ws`/`wss` with default ports 80 and 443; add them to the `-V` protocol list as ADR-0021 requires if that list is built here.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` run `ws://127.0.0.1:<P>/` and a `wss://` variant through fake connectors with the bytes BL-579 and BL-582 measured, pinning request bytes, stdout, stderr and exit code, and `-H`/`-u` reaching the upgrade request as the ADR states.
- [ ] `curl -V` lists `ws` and `wss`, with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
