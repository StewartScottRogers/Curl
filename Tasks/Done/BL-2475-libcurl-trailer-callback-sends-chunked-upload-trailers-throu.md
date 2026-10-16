---
id: BL-2475
title: Libcurl: trailer callback sends chunked-upload trailers through the engine
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2419]
touches: [Libcurl/Callbacks/Phases, Libcurl/CLAUDE.md, Libcurl.UnitTests/Callbacks/Phases, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2475 — Libcurl: trailer callback sends chunked-upload trailers through the engine

## Goal

`CURLOPT_TRAILERFUNCTION` and `CURLOPT_TRAILERDATA` are set through Libcurl, and the engine calls the callback once a chunked HTTP upload's body ends, sending the `Name: value` lines it adds between the closing `0` chunk and the final CRLF; `CURL_TRAILERFUNC_ABORT` fails the perform with `CURLE_ABORTED_BY_CALLBACK`, as curl 8.22.0's `lib/http_chunks.c` (`add_last_chunk`) does.

## Context

Split from BL-2419 (ADR-0611), which wired the prerequest and resolver-start callbacks. The HTTP handler has no way to send request trailers today: `Curl.Protocol.Http`'s `HttpRequestBodyWriter` writes a fixed `0\r\n\r\n` last chunk. Add a seam in `Curl.Protocol.Abstractions` (an upload stream that can supply trailers, as `IUploadRewinder` lets one be rewound), have `HttpRequestBodyWriter` write its lines into the last chunk (HTTP/1.1; HTTP/2 and HTTP/3 send them as a trailing HEADERS frame if the handler supports it, else record what is left as a task), and have `ManagedEasyTransfer`'s read-callback upload stream implement it over `EasyCallbackOptions.Trailer`. Then add the two rows in `Libcurl/Callbacks/Phases` (`EasyPhasesCallbackSetters`). Measure upstream's error text and whether curl adds anything when the callback adds no line before pinning it.

## Acceptance criteria

- [x] An HTTP handler test pins the last chunk with trailers and without, and the abort.
- [x] Engine tests pin the callback's call point and the abort return through `ManagedEasyTransfer`.
- [x] Libcurl sets `TRAILERFUNCTION` (a `TrailerCallback` or `null`, else `BadFunctionArgument`) and `TRAILERDATA` (any value, no change), with tests.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1` reports 0 failing members for every library changed.
- [x] Every new public member has an XML doc comment, and the changed projects' CLAUDE.md files are kept true.

## Notes

- Plan (ADR-0642): new `IUploadTrailerSource` in `Curl.Protocol.Abstractions` beside `IUploadRewinder`
  (`SuppliesTrailers`, `TryTakeTrailers`); `HttpRequestBodyWriter` makes the closing chunk once per send when
  the body's end is first reached (trace or write), with the source's well-formed lines; `CallbackReadStream`
  implements it over `EasyCallbackOptions.Trailer`, set by `ManagedEasyTransfer.CallbackUploadOf`; Libcurl's
  `Callbacks/Phases` adds the `TRAILERFUNCTION` and `TRAILERDATA` rows.
- Upstream text: the curl tool has no option that sets a trailer callback, so it cannot be measured with
  `Record-CurlExchange.ps1`; the bytes and texts come from curl 8.22.0's `add_last_chunk` in
  `lib/http_chunks.c`: abort is `CURLE_ABORTED_BY_CALLBACK` with `operation aborted by trailing headers
  callback`; a callback that adds no line sends the same `0\r\n\r\n` as no callback; a line whose first colon
  is not followed by a space is skipped with `Malformatted trailing header, skipping trailer`; the trace line is
  `http_chunk, added last chunk with trailers from client -> 0`.
- HTTP/2 and HTTP/3: upstream installs the chunked reader only for HTTP/1.x uploads, so it never calls the
  trailer callback there; nothing is left to file.
- Decided: trailer lines are sent as UTF-8; the `-> 42` trace line upstream writes after an abort is not
  written (recorded in ADR-0642).
- `SetOption_TrailerFunction_StillAnswersNotBuiltIn` pinned the old placeholder; it became
  `SetOption_TrailerFunction_SetsTheCallbackAndNullClearsIt`, and `Trailerfunction` and `Trailerdata` joined
  the bad-argument and data rows.
- Touches: added `Libcurl/CLAUDE.md`, whose `Callbacks/Phases` paragraph said `TRAILERFUNCTION` answers
  `NotBuiltIn`; no task in Doing on origin/work/dark-factory names it.
- Measured: `dotnet build` 0 warnings; fast tests 41 projects passed, 0 failed (Http 2109, Engine 1694,
  Libcurl 1087, Console 2806). `Measure-CodeQuality.ps1` for Abstractions, Http, Engine and Libcurl: 1 failing
  member (`HttpRequestBodyWriter.WriteAsync` complexity 12), fixed by extracting `StartSending`; the HTTP
  library's re-measure (which rebuilt and reran its tests) gives 0 failing members, the other three were 0 in
  the first run.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 76246c42 98e69f6c 331a7113 4272a6b9 81cc0a51 1c406d8f.
- 2026-10-16: Doing -> Done. CURLOPT_TRAILERFUNCTION sends a chunked HTTP/1.1 read-callback upload's trailers in the closing chunk; abort fails with CURLE_ABORTED_BY_CALLBACK
