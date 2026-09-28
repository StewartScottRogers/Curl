---
id: BL-558
title: Register the IMAP handler for imap and imaps in Curl.Console
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-539, BL-554, BL-555, BL-556, BL-557]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-558 — Register the IMAP handler for imap and imaps in Curl.Console

## Goal

`curl imap://...` and `curl imaps://...` run end to end through `Curl.Console` with the IMAP handler, the connector and the SASL authenticator, producing the measured request bytes, output and exit codes.

## Context

- Conformance audit 2026-09-28, row 34. Handler: BL-553 to BL-557; context: BL-539.
- Register in `Curl.Console/CurlComposition.cs`; dispatch in `Curl.Core.UnitLibrary/ProtocolDispatcher.cs` must accept `imap`/`imaps` with default ports 143 and 993; add them to the `-V` protocol list as ADR-0021 requires if that list is built here.

## Acceptance criteria

- [ ] `Curl.Console.UnitTests` run `-u u:p "imap://127.0.0.1:<P>/INBOX;UID=1"`, an `imaps://` variant and an `APPEND` upload through fake connectors with the bytes BL-555 and BL-557 measured, pinning request bytes, stdout, stderr and exit code.
- [ ] `curl -V` lists `imap` and `imaps`, with a test.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Console` reports 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
