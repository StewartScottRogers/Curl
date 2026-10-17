---
id: BL-2614
title: Apply CURLOPT_PORT to a proxied FTP URL and finish the FTP transfer for upstream cases 562 and 563
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2614 — Apply CURLOPT_PORT to a proxied FTP URL and finish the FTP transfer for upstream cases 562 and 563

## Goal

Upstream cases 562 and 563 pass through the libtest runner and are on the ratchet.

## Context

Filed by BL-2531 (lib562 ported, `Lib562.cs`, sets `CURLOPT_PORT` from `libtest_arg2`). Case 562 (plain FTP with `;type=A` and `CURLOPT_PORT`) fails: `curl did not finish within 20 seconds`. Case 563 (the same through an HTTP proxy) fails: `<verify><protocol> differs at byte 20 (line 1): expected "GET ftp://127.0.0.1:8993/563;type=A HTTP/1.1
", got "GET ftp://127.0.0.1:23456/563;type=A HTTP/1.1
"` - the port option is not applied to the proxied URL. Run with `CURL_UPSTREAM_CASE=562` / `563`.

## Acceptance criteria

- [ ] Cases 562 and 563 pass through the runner on Windows and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each fix with a focused unit test in the fixed library's `.UnitTests` (ADR-0482).
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
