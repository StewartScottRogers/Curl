---
id: BL-3293
title: Measure the --parallel num_connects and live br size_download differential gaps
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3293 — Measure the --parallel num_connects and live br size_download differential gaps

## Goal

`--parallel` transfers to one host report `%{num_connects}` as real curl does, and the `--compressed -H 'Accept-Encoding: br'` size difference is explained or fixed.

## Context

Found by BL-3235's CLI TLS differential run (live-network cases, also in the tester's `report_tls.txt`). `tls-public-http2-multi` (`-sS --parallel -o NUL -o NUL -w '%{http_version} %{num_connects}\n' https://www.cloudflare.com/ https://www.cloudflare.com/cdn-cgi/trace`, reference build without HTTP/2): real `1.1 1` then `1.1 0`, Curl `1.1 1` twice - real curl's second parallel transfer reused the first's connection (or waited for it). `tls-public-compressed-br`: `%{size_download}` 109162 for Curl against 108854 for real, about 300 bytes more in both runs; the page is live, so first check whether the request headers differ (the response may vary on them) before treating it as a decoding gap. Reproduce on loopback where possible; narrow touches to files once found.

## Acceptance criteria

- [x] Both differences are measured on loopback or explained under Notes, and each real gap is fixed with a unit test or filed as its own task.
- [x] The build is clean and the fast tests pass.

## Notes

- num_connects: real gap. Reproduced on live cloudflare with curl 8.21.0 (real `1.1 1` then `1.1 0`). ParallelHostQueue only holds back http:// transfers, so an https one opens its own connection. Fix lies outside touches (Curl.Console, Curl.Networking); filed as BL-3349.
- br size_download: not a decoding gap. Loopback request bytes with `--compressed -H "Accept-Encoding: br"` are identical for real curl and Curl. Live page size varies per request by hundreds of bytes (raw downloads in one minute: real 109119, Curl 108627; Set-Cookie header lengths differ too). This real curl has no brotli (exit 61, size_download 0), Curl decodes. No fix needed.
- Build clean; Curl.Engine.UnitTests pass (2468, no code changed).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f80c8b28 c46ca32a.
- 2026-10-23: Doing -> Done. Measured both gaps: br size is live-page variance, num_connects filed as BL-3349
