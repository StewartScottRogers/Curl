---
id: BL-5099
title: Ignore alt-svc h3 entries on a build that is not a QUIC build
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/AltSvcTransferCache.cs, Curl.Engine.UnitTests/AltSvcTransferCacheNoQuicTests.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5099 — Ignore alt-svc h3 entries on a build that is not a QUIC build

## Goal

On the Linux and macOS build, alt-svc entries for `h3` are ignored unless the build acts as a QUIC build, so an unreachable h3 alternative no longer ends in exit 28 and the transfer goes over TCP.

## Context

From the outside fitness review of 2026-11-13 (commit 56cb97ede, `preview` build), finding STD-054 (Medium, Confirmed twice; local server and www.cloudflare.com). HTTPS with `--alt-svc`, Linux build. The port's `-V` lists no HTTP3 and `--http3` reports "the installed libcurl version does not support this".

**Repro (Linux).**
1. Start a TLS server on 127.0.0.1:25443 with a certificate for localhost signed by `ca.pem` (PowerShell or a C# file-based app, not Python).
2. `printf 'h1 localhost 25443 h3 localhost 25998 "20271231 00:00:00" 0 0\n' > as.txt`
3. `curl -sS -m 8 --alt-svc as.txt --cacert ca.pem https://localhost:25443/`
4. Optional, run twice: `curl -sS -o /dev/null --alt-svc f -w '%{http_version}\n' https://www.cloudflare.com/`

**Expected (curl 8.18.0, OpenSSL build without QUIC, the port's own Linux reference per ADR-0505 and ADR-0540).** The h3 entry is ignored, the body arrives over TCP, exit 0. Step 4 prints `2` on both runs.

**Actual.** `curl: (28) Connection timed out after 7997 milliseconds`, exit 28 (also with alternative host `10.255.255.1`). Step 4's second run prints `3`. ADR-0226 describes a fast exit 56 for an unreachable alternative, not a wait until `-m`. An h3 entry for the same origin falls back correctly.

**Where to start.** `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs`, `NoVersionOptionDestinations(isWindows)` returns `EveryAlpn` (h1, h2, h3) off Windows. Leave out h3 unless the build acts as a QUIC build (`CommandLineOptions.ActsAsQuicBuild`), as `--http3` is gated. Existing tests: `Curl.Engine.UnitTests/AltSvcTransferCacheTests.cs`, `EngineCompositionAltSvcHttpTests.cs`.

## Acceptance criteria

- [x] New tests in `Curl.Engine.UnitTests/AltSvcTransferCacheNoQuicTests.cs` prove that with a non-QUIC build (Linux/macOS rule) a cache holding an `h3` entry yields no h3 destination when no version option is given, and the h1/h2 entries still apply; with the build acting as a QUIC build the h3 entry is offered as before.
- [x] A test proves the repro: with the `h3 localhost 25998` entry only, the transfer goes straight to the origin over TCP and succeeds (no wait for the alternative).
- [x] Windows behaviour is unchanged (existing tests green); tests are platform-neutral, or pin each platform's answer in its own `[OSCondition]` test.
- [x] `dotnet build` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Mock servers in the review were Python; use PowerShell, a C# file-based app or `Record-CurlExchange.ps1`, never Python.
- Review acceptance check, for a Linux hand check: step 3 exits 0 and prints the body, and `-v` shows no `Alt-svc connecting ... to [h3]` line.

- Decision: ADR-1268. `actsAsQuicBuild` is a new defaulted parameter of `AltSvcTransferCache.OpenAsync` and `LookupOf`; nothing in production sets it. The repro is pinned at the cache level (an h3 entry yields no route and HTTP/1.1). Existing h3-race tests now pass `actsAsQuicBuild: true`. CI on Linux and macOS: tests are platform-neutral (onWindows pinned).

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: a3ba72fb 578c7858 f157ec5c a0b49a29.
- 2026-11-13: Doing -> Done. h3 alt-svc entries ignored without version option on a non-QUIC build (ADR-1268)
