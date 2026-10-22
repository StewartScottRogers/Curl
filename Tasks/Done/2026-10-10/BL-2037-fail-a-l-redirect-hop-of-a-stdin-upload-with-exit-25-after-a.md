---
id: BL-2037
title: Fail a -L redirect hop of a stdin upload with exit 25 after an HTTP/1.0 server's 3xx (upstream test1073, GF-0007)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2037 — Fail a -L redirect hop of a stdin upload with exit 25 after an HTTP/1.0 server's 3xx (upstream test1073, GF-0007)

## Goal

`curl http://host/1073 -T - -L` with standard input, where the server answers the chunked PUT with an HTTP/1.0 3xx, ends with exit 25 `Chunky upload is not supported by HTTP 1.0` before sending the redirected request, as curl 8.21.0 does, so upstream test1073 passes.

## Context

Split from BL-2003 (GF-0007). curl 8.21.0 sends every later request of a transfer as HTTP/1.0 once a response in it was HTTP/1.0 (`http_request_version` uses `data->state.http_neg.rcvd_min`), and `http_req_set_reader` refuses a body of unknown length with no `-H Transfer-Encoding` over HTTP/1.0 with exit 25.

BL-2003 did this for the 401/407 retry inside `HttpProtocolHandler` (`ThrowIfChunkedResendToHttp10`, `HttpRequestFraming.ChunksForUnknownLength`, upstream test1072). A `-L` hop is a new handler call made by `Curl.Core.UnitLibrary/RedirectFollower.cs`, so the fact that the earlier hop's response was HTTP/1.0 has to reach the next hop: e.g. a new `HttpRequestOptions` property (Abstractions) that `RedirectFollower` sets from the hop's result, and that `HttpRequestFraming.Of` treats as HTTP/1.0 for `RefusesUnknownLength`. Today the conformance runner reports: test1073 `<verify><protocol>` differs at line 16: expected the end, got `PUT /newlocation/10730002 HTTP/1.1`.

## Acceptance criteria

- [x] Upstream test1073 passes in `UpstreamConformanceTests.UpstreamCase_RunThroughCurl_HoldsTheRatchet` and 1073 is on `PassingUpstreamCases.txt`.
- [x] A unit test in `Curl.Core.UnitTests` and one in `Curl.Protocol.Http.UnitTests` pin the exit 25 for a redirect hop after an HTTP/1.0 response with a body of unknown length.
- [x] `dotnet build -warnaserror` is clean and the fast tests are green. No option changes, so `--ai-help` is unaffected.

## Notes

- New `HttpRequestOptions.EarlierResponseWasHttp10` (Abstractions). `RedirectFollower` sets it on
  every hop after any hop whose `TransferReport.HttpVersion` was 1.0 (sticky, as curl's
  `http_neg.rcvd_min` is per transfer). `HttpRequestFraming.IsHttp10` treats it as `-0` (so
  `RefusesUnknownLength` fails a chunked stdin hop with exit 25, and no `Expect` is added), and
  `HttpProtocolHandler` formats the hop's request line as HTTP/1.0, the same downgrade test1074's
  pooled connection gets. Chose the request-line downgrade too, not only the refusal, so a
  known-length hop's head stays consistent with curl's HTTP/1.0 request (no Expect, `HTTP/1.0`).
- `Curl.Core.UnitTests` cannot produce exit 25 with its scripted handler, so its test
  (`FollowAsync_AfterAnHttp10Response_SendsEveryLaterHopAsHttp10`) pins that the flag reaches every
  later hop; the exit 25 itself is pinned in `Curl.Protocol.Http.UnitTests`
  (`ExecuteAsync_RedirectHopAfterAnHttp10ResponseWithBodyOfUnknownLength_FailsWithExit25AndSendsNothing`),
  plus a known-length hop that sends an `HTTP/1.0` request line.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. test1073 passes: a -L hop after an HTTP/1.0 response goes out as HTTP/1.0 and a stdin upload fails it with exit 25
