---
id: BL-546
title: Write curl's -v and --trace lines for an SMTP session
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-545]
touches: [Curl.Protocol.Smtp.UnitLibrary, Curl.Protocol.Smtp.UnitTests, Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-546 — Write curl's -v and --trace lines for an SMTP session

## Goal

`-v` and `--trace`/`--trace-ascii` on an SMTP transfer write the same lines curl 8.21.0 writes: the connect lines, each command as `> ` and each reply line as `< `, the TLS lines for STARTTLS, and the closing lines, byte for byte.

## Context

- Conformance audit 2026-09-28, row 34.
- Handlers report through `ITransferEvents` (ADR-0046); `Curl.Output.UnitLibrary/VerboseTransferEventWriter.cs` and `TraceTransferEventWriter.cs` format them. Follow how the FTP handler reports its control-connection lines.
- Measure with `Record-CurlExchange.ps1 -Smtp`: `-v` for an upload, for `AUTH PLAIN` (curl may mask credentials; record what it prints), for STARTTLS with `-k`, and `--trace-ascii -` for the upload.

## Acceptance criteria

- [ ] Measured first as above; stderr (and the trace output) copied into Notes byte for byte, with the parts that vary (ports, times) marked.
- [ ] `Curl.Console.UnitTests` pin the measured `-v` stderr for each case through fake connectors, with varying parts normalised as existing `-v` tests do.
- [ ] A `--trace-ascii` test pins the measured dump for the upload.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
