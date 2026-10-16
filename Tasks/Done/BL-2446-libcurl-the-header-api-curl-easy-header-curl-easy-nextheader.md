---
id: BL-2446
title: Libcurl: the header API (curl_easy_header, curl_easy_nextheader)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2408]
touches: [Libcurl/Headers, Libcurl.UnitTests/Headers, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-16
---
# BL-2446 — Libcurl: the header API (curl_easy_header, curl_easy_nextheader)

## Goal

Libcurl `CurlEasy` has `Header(name, index, origin, request)` and `NextHeader(origin, request, previous)` returning `CurlHeader` (`struct curl_header`) with `CURLH_HEADER/TRAILER/CONNECT/1XX/PSEUDO`, and the managed engine's `ITransferHeaders` records every header of each response, including trailers, 1xx and CONNECT replies.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] Engine tests show recorded headers including a redirect's requests; Libcurl tests pin `CURLHE_*` results.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Design in ADR-0603 (decided by Claude under Stewart's delegation). The engine records what its header output receives, as upstream's `hds_cw_collect_write` client writer does: `HeaderRecordingStream` tees each line into `TransferHeaderStore` ahead of the header callback, for `http`/`https`/`ws`/`wss` performs from the options only. Requests are numbered from the status lines (a status line after a final head starts the next request). Origins: 1xx under a 1xx status, CONNECT when `ConnectReplyHeadWritingEvents` marks the output (`IConnectReplyHeadMarking`), trailer after a final head, `:status` pseudo for HTTP/2 and HTTP/3. `Next` finds its place from the `CurlHeader` it answered before, by reference (`ConditionalWeakTable`), as libcurl's `anchor` does.
- Plan corrected: the seam already had `ITransferHeaders`, `CurlHeader`, `CurlHeaderOrigin` and `CurlHeaderCode` (BL-2315/BL-2293). Libcurl's API is the `CurlEasyHeaders` extension methods `Header` and `NextHeader` in `Libcurl/Headers`, following the WebSocket API's pattern.
- Defaults taken: a negative index is `BadArgument` (upstream's is a `size_t`); a line with no colon, or a fold with no header before it, is dropped rather than failing the transfer; a fold keeps one leading blank (upstream's `unfold_value`). No upstream source or real curl was reachable, so these follow upstream's code as written.
- The replaced `HeadersWebSocketAndConnection_NotYetBuilt_ThrowNotSupported` test is now `WebSocketAndConnection_NotYetBuilt_ThrowNotSupported`: `Headers` no longer throws by design.
- An end-to-end CONNECT test via `ScriptedConnector` was not possible: the scripted connector stands in for the whole tunnel. The CONNECT tagging is pinned at unit level (`ConnectReplyHeadWritingEventsTests`, `HeaderLineTeeStreamTests`, `HeaderRecordingStreamTests`).
- Quality: `Curl.Engine.UnitLibrary` 0 failing members after splitting `StartHead` and `CheckArguments`. Every `Libcurl` member this task added is at 100%. The one failing `Libcurl` member, `CurlMulti.SetOption` (87.5% branch), was already there and this task did not change it; filed as BL-2466.
- Fast tests: all green (Libcurl.UnitTests 931, Curl.Engine.UnitTests 1315, Curl.Conformance.UnitTests 3130 passed).

## Log

- 2026-10-15: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. curl_easy_header and curl_easy_nextheader work through Libcurl's CurlEasyHeaders over the managed engine's recorded headers
