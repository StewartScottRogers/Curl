---
id: BL-1256
title: Fix CI failure ConnectAsync_TracingTheProxyFiltersThroughATunnel_WritesTheTunnelLinesInCurlsOrder on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests, Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-02
completed: 2026-10-02
---
# BL-1256 — Fix CI failure ConnectAsync_TracingTheProxyFiltersThroughATunnel_WritesTheTunnelLinesInCurlsOrder on Linux and macOS

## Goal

`ConnectAsync_TracingTheProxyFiltersThroughATunnel_WritesTheTunnelLinesInCurlsOrder` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_TracingTheProxyFiltersThroughATunnel_WritesTheTunnelLinesInCurlsOrder` failed on Linux and macOS in CI run 37089357581 (https://github.com/StewartScottRogers/Curl/actions/runs/37089357581). First failing commit: 8646f0d0.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 37089357581 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `ConnectAsync_TracingTheProxyFiltersThroughATunnel_WritesTheTunnelLinesInCurlsOrder` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: the tunnel trace tests built their connector with the platform's default build (`HttpProxyTunnelOptions.MatchesSchannelBuild = OperatingSystem.IsWindows()`, ADR-0342). Off Windows that is curl's OpenSSL build, which writes `allocate connect buffer` after `[H1-PROXY] CONNECT start`, so the Windows-measured expectations failed on Linux and macOS. `ConnectAsync_TracingTheSetupFilterThroughATunnel_WritesItsLinesAroundTheTunnel` failed in the same run for the same reason and is fixed by the same change.
- Fix, tests only (the production code was right): `TunnelConnector` and `TraceThroughTunnelAsync` pin the Schannel build by default; the new test `ConnectAsync_TracingTheProxyFiltersThroughATunnelAsTheOpenSslBuild_AllocatesTheConnectBufferAfterConnectStart` pins the OpenSSL build's extra line, so both builds are checked on every platform.
- Verified on Windows: build clean, fast tests green (Networking 2867 passed). With the build pinned the test no longer depends on the platform; the CI half of the criterion is confirmed when the shift integrates this commit (the coordinator's CI watch files a new task if it is not).

## Log

- 2026-10-02: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Proxy tunnel trace tests pin the Schannel build, so they pass on every platform; the OpenSSL line has its own test
