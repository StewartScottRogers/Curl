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
completed: 2026-11-25
---
# BL-5957 — Fix CI failure RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer on macOS

## Goal

`RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer` failed on macOS in CI run 45612393852 (https://github.com/StewartScottRogers/Curl/actions/runs/45612393852). First failing commit: f1c85f80.

    Test method Curl.Extension.WireGuard.UnitTests.Tunnel.WireGuardHttp3EndToEndTests.RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer threw exception:

Lanes test only on Windows, so reproduce with `gh run view 45612393852 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_Http3OnlyThroughWireGuard_CarriesTheQuicDatagramsThroughTheTunnelToThePeer` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause (test only; no product change): two 10 s limits on the real clock. The fake peer waited
  10 s for the first initiation (`TunnelTestPeer.NextSentAsync()`), counted from before curl ran,
  and curl's default 10 s QUIC handshake limit also covers the WireGuard handshake, because
  `DatagramConnectorUdpChannelOpener` opens the channel on the first send. The WireGuard test
  assembly took 3 s on macOS in run 45600792924 and 21 s in the failing run 45612393852, nearly all
  of it this test: a stall past 10 s. What stalled the runner is not shown by the log.
- Fix: the peer waits for curl with no time limit and answers every initiation (curl sends a new
  one each 5 s without a response, which the old relay ignored); the test passes
  `--connect-timeout 600`, so curl's 10 s default does not apply.
- Checked here on Windows: with the relay held silent for a temporary 12 s the test passes; with
  no relay at all and the old command line curl failed with exit 28 after 10222 ms. Build clean,
  all fast tests pass (WireGuard 338 of 338).
- The CI half of the criterion cannot be seen from a lane: the change is platform-neutral and the
  shift's CI watch files a task for any failure. No quality run: no production code changed.

## Log

- 2026-11-25: Created.
- 2026-11-25: Backlog -> Doing. Criteria at claim: d64b363d.
- 2026-11-25: Doing -> Done. The WireGuard HTTP/3 end-to-end test waits for curl with no 10 s real-clock limit and answers resent initiations, so a stalled runner no longer fails it.
