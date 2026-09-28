---
id: BL-552
title: Write curl's -v and --trace lines for a POP3 session
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-551]
touches: [Curl.Protocol.Pop3.UnitLibrary, Curl.Protocol.Pop3.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-552 — Write curl's -v and --trace lines for a POP3 session

## Goal

`-v` and `--trace`/`--trace-ascii` on a POP3 transfer write the same lines curl 8.21.0 writes (connect lines, `> ` commands, `< ` replies, STLS TLS lines, closing lines), byte for byte.

## Context

- Conformance audit 2026-09-28, row 34. Events: `ITransferEvents` (ADR-0046); formatting: `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs`, `TraceTransferEventWriter.cs`.
- Measure with `Record-CurlExchange.ps1 -Pop3`: `-v` for `RETR`, for `USER`/`PASS` (record whether curl masks the password), for `STLS` with `-k`, and `--trace-ascii -` for `RETR`.

## Acceptance criteria

- [ ] Measured first as above; stderr and trace output copied into Notes with varying parts marked.
- [ ] `Curl.Console.UnitTests` pin the measured `-v` stderr for each case and the `--trace-ascii` dump, normalised as existing `-v` tests do.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
