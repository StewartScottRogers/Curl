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
completed:
---
# BL-3913 — Find how upstream test1512's second easy handle reaches server.example.curl under libcurl 8.22.0's per-handle DNS cache

## Goal

Upstream test1512 is back on `PassingUpstreamCases.txt` with Curl still matching libcurl 8.22.0's per-handle DNS cache (ADR-0851), or ADR-0851 records why it stays off.

## Context

BL-3680 (CGR-151, ADR-0851) gave each library easy and multi handle its own DNS cache, as the gap report measured real libcurl 8.22.0 doing and as `dnscache_get` in `lib/vdns/dnscache.c` reads (share, else the handle's own `multi_easy`). test1512 (`Lib1512.cs`) gives only the first of two easy handles `CURLOPT_RESOLVE server.example.curl:<port>:<addr>` and expects both `GET`s, so it came off the ratchet: the harness's second handle cannot resolve the name. How upstream's suite passes it is not explained: `CURLOPT_DNS_USE_GLOBAL_CACHE` is ignored in `lib/setopt.c`, and the vendored copy has no `lib1512.c` to compare the port with. Build upstream's `lib1512.c` against a real libcurl 8.22.0 (or read it from curl's git at curl-8_22_0) and run it against a loopback server: if the second perform fails with 6, the case cannot pass upstream either and stays off; if it passes, find the mechanism and put it in the engine.

## Acceptance criteria

- [ ] Upstream's `lib1512.c` run against real libcurl 8.22.0 is recorded in Notes: each perform's return code and the requests the server saw.
- [ ] Either test1512 is on `PassingUpstreamCases.txt` and passes with `Cgr151Tests` still green, or ADR-0851 says from that measurement why it stays off.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Interactive only: the measurement needs a real libcurl 8.22.0 DLL or shared library, which no lane's worktree reaches (BL-3680 could not). Remove lane: no once such a build sits where a lane may run it.

## Log

- 2026-10-26: Created.
