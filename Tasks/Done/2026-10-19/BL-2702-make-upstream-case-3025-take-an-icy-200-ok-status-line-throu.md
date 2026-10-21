---
id: BL-2702
title: Make upstream case 3025 take an ICY 200 OK status line through CURLOPT_HTTP200ALIASES
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitTests/HttpRequestOptionsMappingTests.cs, Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, Curl.Protocol.Abstractions.UnitTests/HttpRequestOptionsTests.cs, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed: 2026-10-19
---
# BL-2702 — Make upstream case 3025 take an ICY 200 OK status line through CURLOPT_HTTP200ALIASES

## Goal

Upstream case 3025 (lib3025) passes through the runner and is on the ratchet.

## Context

Found by BL-2551, which ported lib3025 (`Curl.Conformance.LibTest.UnitLibrary/Lib3025.cs`): a verbose transfer with `CURLOPT_HEADER` on and `CURLOPT_HTTP200ALIASES` holding `ICY 200 OK`. The server answers `ICY 200 OK`, `Content-Length: 7` and `MooMoo`; real curl takes the alias as a 200 status line, writes headers and body to stdout and ends with 0. Curl writes nothing: the program's stdout against `<reply><data>` differs at byte 0 (line 1): expected `ICY 200 OK\n`, got the end. Find whether the alias list reaches the engine through `Libcurl`'s option setter or the engine's status-line parser ignores it, and add the project at fault to `touches` before any edit. Run with `CURL_UPSTREAM_CASE=3025` and the `NamedUpstreamCase_RunThroughCurl_PassesAsAListedCaseMust` test.

## Acceptance criteria

- [x] Case 3025 passes through the runner on Windows and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [x] A focused unit test in the fixed library's own `.UnitTests` project covers the fix (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-19 (lane 2): Libcurl is not at fault: `EasyHttpOptionSetters.SetHttp200Aliases` already stores the list in `EasyHttpOptions.Http200Aliases`, but nothing in the engine reads it. The fix: add an `Http200Aliases` list to `HttpRequestOptions` (Abstractions), map it in `Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs` beside `AllowHttp09Reply`, and have the HTTP status-line reading (`HttpStatusLine`, `HttpLineReader`, `HttpResponseHeadReader`, `HttpProtocolHandler` near `AcceptsHttp09`) take a line that starts with an alias as an `HTTP/1.0 200` status line (curl's `checkprefixmax` over `CURLOPT_HTTP200ALIASES`), so it is neither refused as HTTP/0.9 nor treated as body. `touches` changed from Libcurl and Libcurl.UnitTests to these files. Requeued before any edit: BL-2714 (Doing) holds the whole of Curl.Engine.UnitLibrary and Curl.Engine.UnitTests.

- 2026-10-19 (lane 3): Fixed. `HttpRequestOptions.Http200Aliases` carries `EasyHttpOptions.Http200Aliases` (`HttpRequestOptionsMapping.Of`) to `HttpResponseHeadReader.Http200Aliases`, which hands it to `HttpLineReader`. `HttpStatusLine.CanBeginStatusLine` keeps bytes that can still begin an alias from being refused as HTTP/0.9 (curl 8.22.0 `checkhttpprefix` checks the aliases first), `MatchesAlias` compares as far as the shorter of line and alias goes in any letter case (`checkprefixmax`), and a matching line is `Http200Alias()`, `HTTP/1.0 200`, with the `HTTP 1.0, assume close after body` info line as curl sets `httpversion` 10. Focused tests: `HttpStatusLineTests` (alias rejection, `MatchesAlias`, `Http200Alias`) and `HttpProtocolHandlerTests.ExecuteAsync_Http200AliasStatusLine_IsReadAsHttp10Status200`, plus the record and mapping tests. Measure-CodeQuality over the three changed libraries: 0 failing members. No ADR: the behaviour is curl's, nothing chosen.

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 70504463 48be2d2b 3d836d5e.
- 2026-10-19: Doing -> Backlog. Needs Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs and its tests, which BL-2714 (Doing) holds as whole Curl.Engine projects
- 2026-10-19: Backlog -> Doing. Criteria at claim: 70504463 48be2d2b 3d836d5e.
- 2026-10-19: Doing -> Done. Upstream case 3025 passes: a CURLOPT_HTTP200ALIASES status line such as ICY 200 OK is read as HTTP/1.0 200
