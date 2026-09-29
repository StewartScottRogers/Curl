---
id: BL-559
title: Write curl's -v and --trace lines for an IMAP session
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-558]
touches: [Curl.Protocol.Imap.UnitLibrary, Curl.Protocol.Imap.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-559 — Write curl's -v and --trace lines for an IMAP session

## Goal

`-v` and `--trace`/`--trace-ascii` on an IMAP transfer write the same lines curl 8.21.0 writes (connect lines, `> ` tagged commands, `< ` responses, literal handling, STARTTLS TLS lines, closing lines), byte for byte.

## Context

- Conformance audit 2026-09-28, row 34. Events: `ITransferEvents` (ADR-0046); formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TraceTransferEventWriter.cs`.
- Measure with `Record-CurlExchange.ps1 -Imap`: `-v` for a `UID FETCH`, for `LOGIN` (record whether curl masks the password), for an `APPEND`, for STARTTLS with `-k`, and `--trace-ascii -` for the fetch.

## Acceptance criteria

- [ ] Measured first as above; stderr and trace output copied into Notes with varying parts marked.
- [ ] `Curl.Console.UnitTests` pin the measured `-v` stderr for each case and the `--trace-ascii` dump, normalised as existing `-v` tests do.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
