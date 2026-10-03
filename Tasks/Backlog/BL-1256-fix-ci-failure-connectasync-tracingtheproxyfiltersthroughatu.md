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
completed:
---
# BL-1256 — Fix CI failure ConnectAsync_TracingTheProxyFiltersThroughATunnel_WritesTheTunnelLinesInCurlsOrder on Linux and macOS

## Goal

`ConnectAsync_TracingTheProxyFiltersThroughATunnel_WritesTheTunnelLinesInCurlsOrder` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `ConnectAsync_TracingTheProxyFiltersThroughATunnel_WritesTheTunnelLinesInCurlsOrder` failed on Linux and macOS in CI run 37089357581 (https://github.com/StewartScottRogers/Curl/actions/runs/37089357581). First failing commit: 8646f0d0.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 37089357581 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `ConnectAsync_TracingTheProxyFiltersThroughATunnel_WritesTheTunnelLinesInCurlsOrder` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-10-02: Created.
