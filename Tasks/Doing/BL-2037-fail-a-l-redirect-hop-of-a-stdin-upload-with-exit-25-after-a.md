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
completed:
---
# BL-2037 — Fail a -L redirect hop of a stdin upload with exit 25 after an HTTP/1.0 server's 3xx (upstream test1073, GF-0007)

## Goal

`curl http://host/1073 -T - -L` with standard input, where the server answers the chunked PUT with an HTTP/1.0 3xx, ends with exit 25 `Chunky upload is not supported by HTTP 1.0` before sending the redirected request, as curl 8.21.0 does, so upstream test1073 passes.

## Context

Split from BL-2003 (GF-0007). curl 8.21.0 sends every later request of a transfer as HTTP/1.0 once a response in it was HTTP/1.0 (`http_request_version` uses `data->state.http_neg.rcvd_min`), and `http_req_set_reader` refuses a body of unknown length with no `-H Transfer-Encoding` over HTTP/1.0 with exit 25.

BL-2003 did this for the 401/407 retry inside `HttpProtocolHandler` (`ThrowIfChunkedResendToHttp10`, `HttpRequestFraming.ChunksForUnknownLength`, upstream test1072). A `-L` hop is a new handler call made by `Curl.Core.UnitLibrary/RedirectFollower.cs`, so the fact that the earlier hop's response was HTTP/1.0 has to reach the next hop: e.g. a new `HttpRequestOptions` property (Abstractions) that `RedirectFollower` sets from the hop's result, and that `HttpRequestFraming.Of` treats as HTTP/1.0 for `RefusesUnknownLength`. Today the conformance runner reports: test1073 `<verify><protocol>` differs at line 16: expected the end, got `PUT /newlocation/10730002 HTTP/1.1`.

## Acceptance criteria

- [ ] Upstream test1073 passes in `UpstreamConformanceTests.UpstreamCase_RunThroughCurl_HoldsTheRatchet` and 1073 is on `PassingUpstreamCases.txt`.
- [ ] A unit test in `Curl.Core.UnitTests` and one in `Curl.Protocol.Http.UnitTests` pin the exit 25 for a redirect hop after an HTTP/1.0 response with a body of unknown length.
- [ ] `dotnet build -warnaserror` is clean and the fast tests are green. No option changes, so `--ai-help` is unaffected.

## Notes

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
