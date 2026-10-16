---
id: BL-2475
title: Libcurl: trailer callback sends chunked-upload trailers through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2419]
touches: [Libcurl/Callbacks/Phases, Libcurl.UnitTests/Callbacks/Phases, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-16
completed:
---
# BL-2475 — Libcurl: trailer callback sends chunked-upload trailers through the engine

## Goal

`CURLOPT_TRAILERFUNCTION` and `CURLOPT_TRAILERDATA` are set through Libcurl, and the engine calls the callback once a chunked HTTP upload's body ends, sending the `Name: value` lines it adds between the closing `0` chunk and the final CRLF; `CURL_TRAILERFUNC_ABORT` fails the perform with `CURLE_ABORTED_BY_CALLBACK`, as curl 8.22.0's `lib/http_chunks.c` (`add_last_chunk`) does.

## Context

Split from BL-2419 (ADR-0611), which wired the prerequest and resolver-start callbacks. The HTTP handler has no way to send request trailers today: `Curl.Protocol.Http`'s `HttpRequestBodyWriter` writes a fixed `0\r\n\r\n` last chunk. Add a seam in `Curl.Protocol.Abstractions` (an upload stream that can supply trailers, as `IUploadRewinder` lets one be rewound), have `HttpRequestBodyWriter` write its lines into the last chunk (HTTP/1.1; HTTP/2 and HTTP/3 send them as a trailing HEADERS frame if the handler supports it, else record what is left as a task), and have `ManagedEasyTransfer`'s read-callback upload stream implement it over `EasyCallbackOptions.Trailer`. Then add the two rows in `Libcurl/Callbacks/Phases` (`EasyPhasesCallbackSetters`). Measure upstream's error text and whether curl adds anything when the callback adds no line before pinning it.

## Acceptance criteria

- [ ] An HTTP handler test pins the last chunk with trailers and without, and the abort.
- [ ] Engine tests pin the callback's call point and the abort return through `ManagedEasyTransfer`.
- [ ] Libcurl sets `TRAILERFUNCTION` (a `TrailerCallback` or `null`, else `BadFunctionArgument`) and `TRAILERDATA` (any value, no change), with tests.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1` reports 0 failing members for every library changed.
- [ ] Every new public member has an XML doc comment, and the changed projects' CLAUDE.md files are kept true.

## Notes

## Log

- 2026-10-16: Created.
