---
id: BL-5614
title: Fill CurlResponse.Info.HttpVersion before Curl.Api SendAsync returns
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Messages/CurlClient.SendAsync.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5614 — Fill CurlResponse.Info.HttpVersion before Curl.Api SendAsync returns

## Goal

`CurlResponse.Info.HttpVersion` holds the response's HTTP version as soon as `SendAsync` returns, not 0.

## Context

From Surly cookbook bug report `2026-11-20T1022-Curl-bug-report.md` (batch 4 of the 1,000-recipe plan), finding 3. Build tested: the Curl port at `b9ca725077` (`curl`, `curl-mcp 0.1.0`, `Curl.Api 0.2.0`, `Libcurl 0.2.0`), Windows 11; server Surl `763754785`. Check it still reproduces on the current branch first.

`CurlResponse.Info.HttpVersion` is 0 when read right after `SendAsync`. The version is known once the response head is read; check which other `Info` values are also filled only later (after the body is read or the response disposed) and document when each is final, or fill them at head time as libcurl's `CURLINFO_HTTP_VERSION` is available after the headers.

## Acceptance criteria

- [ ] Right after `SendAsync` returns, `Info.HttpVersion` is the version of the response (1.1, 2 or 3) against test servers for each, pinned by tests in `Curl.Api.UnitTests`.
- [ ] `CurlResponse.Info`'s doc comments say which values are final when `SendAsync` returns and which only after the body is read.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created from Surly bug report 2026-11-20T1022, finding 3.
