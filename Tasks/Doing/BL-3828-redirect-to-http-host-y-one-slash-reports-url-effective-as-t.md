---
id: BL-3828
title: Redirect to http:/host/y (one slash) reports url_effective as the raw text where curl normalises it to http://host/y
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Protocol.Http.UnitTests/HttpRedirectLocationSingleSlashTests.cs]
requirement: none
created: 2026-10-26
completed:
---
# BL-3828 — Redirect to http:/host/y (one slash) reports url_effective as the raw text where curl normalises it to http://host/y

## Goal

`curl -sS -L` following `Location: http:/127.0.0.1:PORT/y` writes `%{url_effective}` as `http://127.0.0.1:PORT/y`, as curl 8.22.0 does.

## Context

Found by BL-3547 (CGR-018), measured with Record-CurlExchange.ps1 on 2026-10-26: a 302 with `Location: http:/127.0.0.1:18731/y` then 200 `ok`, `-sS -L http://127.0.0.1:18731/a -w "[%{url_effective}][%{redirect_url}]"`. curl 8.22.0 (Windows): exit 0, stdout `ok[http://127.0.0.1:18731/y][]`, GET /a then GET /y. Curl: the same requests and exit, but stdout `ok[http:/127.0.0.1:18731/y][]`. Start at `HttpRedirectLocation.NormalizeAbsolute` (an absolute target with fewer than two slashes after the scheme) and the effective URL `RedirectFollower` reports.

## Acceptance criteria

- [ ] The repro above gives curl 8.22.0's stdout, exit code and request bytes through Curl; commands and both results are in Notes.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project fails before the fix and passes after it.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: d034ce34 0ee60237 779790c0.
