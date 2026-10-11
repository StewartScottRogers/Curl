---
id: BL-2086
title: Carry the HTTP/1.0 downgrade into the auth retry in HttpProtocolHandler.RetryOfAsync (upstream test1071)
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-11
completed:
---
# BL-2086 — Carry the HTTP/1.0 downgrade into the auth retry in HttpProtocolHandler.RetryOfAsync (upstream test1071)

## Goal

Curl keeps the HTTP/1.0 downgrade when it retries a request after an authentication challenge, so upstream test1071 passes.

## Context

Upstream test1071 uploads with `-T` to a server that answers HTTP/1.0 with a 401 and expects the authenticated retry PUT to go out as HTTP/1.0 too; Curl sends it as HTTP/1.1. `HttpProtocolHandler.RetryOfAsync` (Curl.Protocol.Http.UnitLibrary, about lines 2346-2350) does not carry the downgrade the first response triggered into the retry. Read 8.21.0 `tests/data/test1071` and measure real curl with `Record-CurlExchange.ps1` first. Its `touches` overlap BL-2085's, so the board keeps them apart; no dependency is needed.

Source: re-measurement of the gap findings with the fixed gap harness (PR #119, 2026-10-11); parent re-close task BL-2074. Targeted curl version: 8.21.0 (tests/data in the release tarball).

## Acceptance criteria

- [ ] A new MSTest in Curl.Protocol.Http.UnitTests (no network) shows that after an HTTP/1.0 response to the first request, the authentication retry's request line ends `HTTP/1.0`, with the request bytes test1071 expects.
- [ ] A neighbouring test shows a retry after an HTTP/1.1 response still goes out as HTTP/1.1.
- [ ] Add test1071 to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` once it passes in process.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing.
