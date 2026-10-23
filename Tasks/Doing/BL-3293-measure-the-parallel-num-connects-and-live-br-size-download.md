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
completed:
---
# BL-3293 — Measure the --parallel num_connects and live br size_download differential gaps

## Goal

`--parallel` transfers to one host report `%{num_connects}` as real curl does, and the `--compressed -H 'Accept-Encoding: br'` size difference is explained or fixed.

## Context

Found by BL-3235's CLI TLS differential run (live-network cases, also in the tester's `report_tls.txt`). `tls-public-http2-multi` (`-sS --parallel -o NUL -o NUL -w '%{http_version} %{num_connects}\n' https://www.cloudflare.com/ https://www.cloudflare.com/cdn-cgi/trace`, reference build without HTTP/2): real `1.1 1` then `1.1 0`, Curl `1.1 1` twice - real curl's second parallel transfer reused the first's connection (or waited for it). `tls-public-compressed-br`: `%{size_download}` 109162 for Curl against 108854 for real, about 300 bytes more in both runs; the page is live, so first check whether the request headers differ (the response may vary on them) before treating it as a decoding gap. Reproduce on loopback where possible; narrow touches to files once found.

## Acceptance criteria

- [ ] Both differences are measured on loopback or explained under Notes, and each real gap is fixed with a unit test or filed as its own task.
- [ ] The build is clean and the fast tests pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f80c8b28 c46ca32a.
