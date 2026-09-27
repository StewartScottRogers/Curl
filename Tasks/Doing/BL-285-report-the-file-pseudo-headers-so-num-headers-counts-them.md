---
id: BL-285
title: Report the file:// pseudo-headers so %{num_headers} counts them
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.File.UnitLibrary, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-285 — Report the file:// pseudo-headers so %{num_headers} counts them

## Goal

The file:// handler returns a `TransferReport` whose `ResponseHeaders` hold the three pseudo-headers curl 8.21.0 counts, so `%{num_headers}` prints 3 as curl does.

## Context

- Found by BL-225. Measured 2026-09-26: `curl -s -o NUL -D - -w "[%header{Content-Length}][%{num_headers}]" file:///C:/Windows/win.ini` wrote `Content-Length: 92`, `Accept-ranges: bytes`, `Last-Modified: Mon, 01 Apr 2024 07:24:04 GMT`, then `[][3]`. So curl counts three headers but `%header{Content-Length}` finds none - measure why before choosing what the report carries (curl's header API may file them under a different origin).
- Today the file handler returns no report, so `TransferWriteOutVariables` prints `%{num_headers}` as 0.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009, ADR-0018) - against a loopback server (`Record-CurlExchange.ps1`), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [ ] A file:// transfer's `%{num_headers}` is 3 and its `%header{Content-Length}` empty, as measured on curl 8.21.0, pinned in a file-handler test.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.File`.

## Notes

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
