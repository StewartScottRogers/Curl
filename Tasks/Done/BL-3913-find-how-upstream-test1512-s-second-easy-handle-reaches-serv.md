---
id: BL-3913
title: Find how upstream test1512's second easy handle reaches server.example.curl under libcurl 8.22.0's per-handle DNS cache
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.LibTest.UnitLibrary/Lib1512.cs]
lane: no
requirement: none
created: 2026-10-26
completed: 2026-10-29
---
# BL-3913 — Find how upstream test1512's second easy handle reaches server.example.curl under libcurl 8.22.0's per-handle DNS cache

## Goal

Upstream test1512 is back on `PassingUpstreamCases.txt` with Curl still matching libcurl 8.22.0's per-handle DNS cache (ADR-0851), or ADR-0851 records why it stays off.

## Context

BL-3680 (CGR-151, ADR-0851) gave each library easy and multi handle its own DNS cache, as the gap report measured real libcurl 8.22.0 doing and as `dnscache_get` in `lib/vdns/dnscache.c` reads (share, else the handle's own `multi_easy`). test1512 (`Lib1512.cs`) gives only the first of two easy handles `CURLOPT_RESOLVE server.example.curl:<port>:<addr>` and expects both `GET`s, so it came off the ratchet: the harness's second handle cannot resolve the name. How upstream's suite passes it is not explained: `CURLOPT_DNS_USE_GLOBAL_CACHE` is ignored in `lib/setopt.c`, and the vendored copy has no `lib1512.c` to compare the port with. Build upstream's `lib1512.c` against a real libcurl 8.22.0 (or read it from curl's git at curl-8_22_0) and run it against a loopback server: if the second perform fails with 6, the case cannot pass upstream either and stays off; if it passes, find the mechanism and put it in the engine.

## Acceptance criteria

- [x] Upstream's `lib1512.c` run against real libcurl 8.22.0 is recorded in Notes: each perform's return code and the requests the server saw.
- [x] Either test1512 is on `PassingUpstreamCases.txt` and passes with `Cgr151Tests` still green, or ADR-0851 says from that measurement why it stays off.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Interactive only: the measurement needs a real libcurl 8.22.0 DLL or shared library, which no lane's worktree reaches (BL-3680 could not). Remove lane: no once such a build sits where a lane may run it.
- 2026-10-29 measurement: upstream `lib1512.c` at `curl-8_22_0`, translated call for call to P/Invoke (a C# file-based app; no C compiler here), run against `C:\UpstreamCurl\curl-8.22.0_2-win64-mingw\bin\libcurl-x64.dll` (`libcurl/8.22.0 LibreSSL/4.3.2`) with a loopback server sending test1512's reply. Both `CURLOPT_DNS_USE_GLOBAL_CACHE` setopts return 0; perform 1 returns 0 (`GET /path/15120001`); perform 2 returns 6 (`Could not resolve host: server.example.curl`). The server saw one request; test1512 expects two.
- Why upstream "passes": it does not run it. `tests/data/DISABLED` has listed 1512 since curl commit 4b0028f82d (2013-08-10) and still does at `curl-8_22_0`. ADR-0851 now records this; test1512 stays off `PassingUpstreamCases.txt`, and `Lib1512.cs` is unchanged.

## Log

- 2026-10-26: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 62dddd11 35ac9927 779790c0.
- 2026-10-29: Doing -> Done. Measured lib1512 on real libcurl 8.22.0: perform 2 fails with 6, upstream DISABLED skips test1512; ADR-0851 records why it stays off
