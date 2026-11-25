---
id: BL-5957
title: Fix CI failure RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer on macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.WireGuard.UnitTests, Curl.Extension.WireGuard.UnitLibrary]
requirement: none
created: 2026-11-25
completed:
---
# BL-5957 — Fix CI failure RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer on macOS

## Goal

`RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer` failed on macOS in CI run 45612393852 (https://github.com/StewartScottRogers/Curl/actions/runs/45612393852). First failing commit: f1c85f80.

    Test method Curl.Extension.WireGuard.UnitTests.Tunnel.WireGuardHttp3EndToEndTests.RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer threw exception:

Lanes test only on Windows, so reproduce with `gh run view 45612393852 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [ ] `RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: d64b363d.
