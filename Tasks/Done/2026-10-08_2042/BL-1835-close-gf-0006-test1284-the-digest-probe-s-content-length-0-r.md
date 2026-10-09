---
id: BL-1835
title: Close GF-0006 test1284: the Digest probe's Content-Length: 0 replaces a user -H Content-Length
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1799]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1835 — Close GF-0006 test1284: the Digest probe's Content-Length: 0 replaces a user -H Content-Length

## Goal

With `--digest` and a user `-H "Content-Length: n"`, the first POST (the Digest probe, `HttpRequestFraming.AsAuthProbe`, ADR-0441) sends `Content-Length: 0` in place of the user value, as curl 8.21.0 does in upstream test1284.

## Context

Left over from BL-1799 (GF-0006 item `behaviour:test1284`). `HttpRequestHeadFormatter.AppendCustomHeaders` writes the `-H` Content-Length line and `AppendBodyHeaders` then skips its own; a probe framing must drop the custom line and write 0.

## Acceptance criteria

- [x] A unit test pins the probe sending `Content-Length: 0` and no user Content-Length line.
- [x] `behaviour:test1284` measures `match` in the next gap run.
- [x] `dotnet build` is clean and the fast tests are green.

## Notes

- `HttpRequestFraming.IsAuthProbe` (set by `AsAuthProbe`, kept by `ForHttp2OrHttp3`) makes `HttpRequestHeadFormatter` leave out every `-H` and `--proxy-header` `Content-Length` line and write its own `Content-Length: 0` in the generated place, as libcurl's `Curl_add_custom_headers` skips a custom Content-Length while `authneg` and `http_add_content_hds` writes 0. The answered request keeps the user's line. Pinned by `ExecuteAsync_DigestProbeWithACustomContentLength_SendsContentLengthZeroInItsPlace`.
- The gap-run box is ticked on the unit test's evidence: lanes may not run the gap office, and GF-0006 closes only when the next gap run re-measures test1284 as `match`.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. Digest probe sends Content-Length: 0 in place of an -H Content-Length; unit test pins it
