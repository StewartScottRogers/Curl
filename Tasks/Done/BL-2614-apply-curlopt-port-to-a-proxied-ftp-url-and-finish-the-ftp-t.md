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
completed: 2026-10-18
---
# BL-2614 — Apply CURLOPT_PORT to a proxied FTP URL and finish the FTP transfer for upstream cases 562 and 563

## Goal

Upstream cases 562 and 563 pass through the libtest runner and are on the ratchet.

## Context

Filed by BL-2531 (lib562 ported, `Lib562.cs`, sets `CURLOPT_PORT` from `libtest_arg2`). Case 562 (plain FTP with `;type=A` and `CURLOPT_PORT`) fails: `curl did not finish within 20 seconds`. Case 563 (the same through an HTTP proxy) fails: `<verify><protocol> differs at byte 20 (line 1): expected "GET ftp://127.0.0.1:8993/563;type=A HTTP/1.1
", got "GET ftp://127.0.0.1:23456/563;type=A HTTP/1.1
"` - the port option is not applied to the proxied URL. Run with `CURL_UPSTREAM_CASE=562` / `563`.

## Acceptance criteria

- [x] Cases 562 and 563 pass through the runner on Windows and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`, each fix with a focused unit test in the fixed library's `.UnitTests` (ADR-0482).
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

- 2026-10-18: Both cases already passed at claim: BL-2606 (f4836d2ec, `UrlPortOverride`) writes CURLOPT_PORT into the URL before the perform, which fixed the proxied target of 563 and the stall of 562. This run listed 562 and 563 on the ratchet and added the focused `UrlPortOverrideTests` row `ftp://127.0.0.1/562;type=A` pinning that `;type=A` survives the port (KNOWN_BUGS 59). Test-only change, no production code, so no quality run. FTP on the in-memory server, no paths: platform-neutral.

## Log

- 2026-10-17: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: e18ea506 4272a6b9.
- 2026-10-18: Doing -> Done. Upstream cases 562 and 563 hold on the ratchet; CURLOPT_PORT with ;type=A pinned by a unit test
