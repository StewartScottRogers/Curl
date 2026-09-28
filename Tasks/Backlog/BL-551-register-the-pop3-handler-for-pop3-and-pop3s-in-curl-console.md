---
id: BL-551
title: Register the POP3 handler for pop3 and pop3s in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-539, BL-548, BL-549, BL-550]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-551 — Register the POP3 handler for pop3 and pop3s in Curl.Console

## Goal

`curl pop3://...` and `curl pop3s://...` run end to end through `Curl.Console` with the POP3 handler, the connector and the SASL authenticator, producing the measured request bytes, output and exit codes.

## Context

- Conformance audit 2026-09-28, row 34. Handler: BL-547 to BL-550; context: BL-539.
- Register in `Curl.Console/CurlComposition.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `pop3`/`pop3s` with default ports 110 and 995; add them to the `-V` protocol list as ADR-0021 requires if that list is built here.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` run `-u u:p pop3://127.0.0.1:<P>/1` and a `pop3s://` variant through fake connectors with the bytes BL-549 measured, pinning request bytes, stdout, stderr and exit code.
- [ ] `curl -V` lists `pop3` and `pop3s`, with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
