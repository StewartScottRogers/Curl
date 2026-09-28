---
id: BL-516
title: Report %{size_delivered} as the bytes delivered after decoding, apart from %{size_download}
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Output.UnitLibrary, Curl.Output.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-516 — Report %{size_delivered} as the bytes delivered after decoding, apart from %{size_download}

## Goal

`%{size_delivered}` and `%{size_download}` print what curl 8.21.0 prints for each, which differ when the body is decoded (`--compressed`, chunked framing), instead of both coming from one source.

## Context

- Conformance audit 2026-09-28, row 43 (Major, "measure first"; sequenced into the opening queue at Stewart's request).
- `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs` maps both `size_download` and `size_delivered` to `DownloadSize`. `--compressed` decoding is in `Curl.Protocol.Http.UnitLibrary` (ADR-0020, ADR-0031, ADR-0070); what the progress reports count is ADR-0065.
- Do not assume which of the two is the wire count: measure it.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: a gzip body with `--compressed -w '%{size_download} %{size_delivered}'`, the same without `--compressed`, a chunked body, a plain body, and `-w '%{json}'`; stdout copied into Notes with the byte sizes of the canned responses.
- [ ] The source of each variable is named in the XML docs, and `Curl.Output.UnitTests` pin both variables from distinct values.
- [ ] `Curl.Console.UnitTests` (or `Curl.Protocol.Http.UnitTests`) pin each measured case end to end.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
