---
id: BL-619
title: Save and compare ETags with --etag-save and --etag-compare
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-619 — Save and compare ETags with --etag-save and --etag-compare

## Goal

`--etag-save <file>` writes the response's `ETag` value to the file, and `--etag-compare <file>` sends its content as `If-None-Match`, with the file handling, missing-file behaviour and output curl 8.21.0 has.

## Context

- Conformance audit 2026-09-28, row 19 (Major, S-M).
- Rows `etag-save` and `etag-compare` in `Curl.Cli.UnitLibrary/CurlOptionAliasTable.cs`. Custom headers reach the HTTP request through `Curl.Console/HttpRequestOptionsMapping.cs`; response headers are on `TransferReport.ResponseHeaders`. File access goes through the console's existing file seams.
- Measure: where `If-None-Match` sits among the headers, what is written when there is no `ETag`, a missing compare file, a `304` reply, and both options naming one file.

## Acceptance criteria

- [ ] Measured first with `Record-CurlExchange.ps1`: the cases above; request bytes, stdout, stderr, exit code and the saved file copied into Notes.
- [ ] `Curl.Cli.UnitTests` cover parsing; `Curl.Console.UnitTests` pin each measured case through fake file seams.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-28: Created.
