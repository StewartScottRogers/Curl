---
id: BL-2268
title: Write the headers and body before %{header_json} on an HTTP/3 transfer so upstream case 2503 passes on Linux and macOS
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Http3.UnitLibrary, Curl.Http3.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-15
completed:
---
# BL-2268 — Write the headers and body before %{header_json} on an HTTP/3 transfer so upstream case 2503 passes on Linux and macOS

## Goal

Upstream case 2503 (`-w '%{header_json}\n' --http3-only` against the nghttpx-h3 stand-in on `%HTTP3PORT`) writes what curl 8.22.0 does on Linux and macOS, and is listed in `PassingUpstreamCases.txt`.

## Context

BL-2264 (2026-10-15) made the conformance harness report `headers-api`, so 2503 runs on Linux and macOS (on Windows it is `SkippedOnThisPlatform` for `http/3`). Measured on Windows emulating the Linux build (temporary `UpstreamCurlPlatform.Unix` with `parsesAsWindowsBuild: false` in `UpstreamConformanceTests`, as BL-2261 did; reverted): `<verify><stdout>` differs at byte 0, expected `HTTP/3 200 \r\n` and got `{\n` - the header block and the `-foo-` body that runtests.pl's `--include` should put before the JSON are missing, though the same command over HTTP/2 (test2403) passes. Start by running 2503 under that emulation with `CURL_UPSTREAM_KEEP_LOGS=1` and reading `trace2503` to see whether the response arrives at all. Correct `touches` once the cause is found.

## Acceptance criteria

- [ ] The cause is fixed with a focused unit test in the fixed library's own `.UnitTests` project (ADR-0482).
- [ ] 2503 passes under the Linux emulation and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] The fast tests are green.

## Notes

## Log

- 2026-10-15: Created.
