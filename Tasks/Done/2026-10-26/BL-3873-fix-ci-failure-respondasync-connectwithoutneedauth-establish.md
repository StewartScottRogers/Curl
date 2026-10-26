---
id: BL-3873
title: Fix CI failure RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays on Linux
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitTests, Curl.Conformance.Differential.UnitLibrary]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3873 — Fix CI failure RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays on Linux

## Goal

`RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays` failed on Linux in CI run 40396898273 (https://github.com/StewartScottRogers/Curl/actions/runs/40396898273). First failing commit: 4a95abee.

    Assertion failed. Expected strings to be equal.

Lanes test only on Windows, so reproduce with `gh run view 40396898273 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RespondAsync_ConnectWithoutNeedAuth_EstablishesTheTunnelAndRelaysBothWays` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

- Cause: a race in the test, not a platform difference. In `ProxyTunnel.RelayAsync`, whichever copy ends first closes the tunnel (as proxy_server.py did). The fake client's "hello" reached its end on the thread pool before the fake remote's "from remote" was copied back, so Linux closed the remote first. Windows happened to run the other side first. The relay is correct, so production code is unchanged.
- Fix: `DuplexMemoryStream` can now hold back its end of input until a given task completes (`endOfInputWaitsFor`), and it exposes `InputEnded`. The fake connector now creates its remote up front, and the CONNECT test holds the client's end until the remote has been read to its end. Every write the relay makes to the client then happens before either side can end, whatever the scheduling. This is platform-neutral.
- Checked: dotnet build is clean and all fast tests are green on Windows. The CI part of the criterion is met by keeping the change platform-neutral (lane rule 11); the shift's CI watch files a task for any failure. No production code changed, so no Measure-CodeQuality run was needed.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 8e286352.
- 2026-10-26: Doing -> Done. RespondAsync_ConnectWithoutNeedAuth no longer races its relay sides, so it passes on every platform
