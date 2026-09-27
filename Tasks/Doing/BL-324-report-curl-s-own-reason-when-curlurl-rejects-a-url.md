---
id: BL-324
title: Report curl's own reason when CurlUrl rejects a URL
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-294]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-324 — Report curl's own reason when CurlUrl rejects a URL

## Goal

When `CurlUrl` rejects a transfer URL, the exit-3 line names the reason curl 8.21.0
names (its `CURLUcode` message) instead of always `Malformed input to a URL function`.

## Context

- BL-294 switched `CurlCommandRunner` to `CurlUrl.TryParse`, which answers only
  true or false, so every rejection prints `curl: (3) URL rejected: Malformed input to a
  URL function` (ADR-0010, "Switch decisions taken under BL-294").
- Measured on 2026-09-27 against curl 8.21.0 (`/mingw64/bin/curl`):
  `curl -sS file://example.com/x` and `curl -sS "file://[::1]/x"` print
  `curl: (3) URL rejected: Bad file:// URL`; `curl -sS "http:////h/"` prints
  `curl: (3) URL rejected: Unsupported number of slashes following scheme`.
  Before BL-294 the `file` handler printed `Bad file:// URL` for the first two.
- Messages are libcurl's `curl_url_strerror` texts (`lib/urlapi.c`, `lib/strerror.c`
  at 8.21.0). Measure each rejection class before pinning its text.
- Start at `CurlUrlParser` (each `return null`), `CurlUrl.TryParse`, and
  `CurlCommandRunner`'s `MalformedUrlMessage`.

## Acceptance criteria

- [ ] `CurlUrl` exposes why it rejected a text (for example a `TryParse` overload with an `out` reason), without changing the existing `TryParse(string, bool, out CurlUrl)`.
- [ ] `curl -sS file://example.com/x` run through `CurlCommandRunner` exits 3 with `curl: (3) URL rejected: Bad file:// URL`, pinned by a test in `Curl.Console.UnitTests`.
- [ ] `http:////h/` exits 3 with `curl: (3) URL rejected: Unsupported number of slashes following scheme`, pinned by a test.
- [ ] Every other rejection class `CurlUrlParser` has is measured against curl 8.21.0 and pinned, or keeps `Malformed input to a URL function` where curl prints that.
- [ ] `dotnet build` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage for the touched libraries.

## Notes

- Filed by BL-294.

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
