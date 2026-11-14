---
id: BL-5152
title: Fix CI failure RunAsync_H3EntryForTheOriginAndQuicFails_FallsBackToTcpAndWritesTheRenewedEntry on Linux and macOS
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests, Curl.Console, Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5152 — Fix CI failure RunAsync_H3EntryForTheOriginAndQuicFails_FallsBackToTcpAndWritesTheRenewedEntry on Linux and macOS

## Goal

`RunAsync_H3EntryForTheOriginAndQuicFails_FallsBackToTcpAndWritesTheRenewedEntry` passes on Windows, Linux and macOS, so the `CI` workflow on the shift's branch is green again.

## Context

Filed by the dark factory's CI watch (BL-987). `RunAsync_H3EntryForTheOriginAndQuicFails_FallsBackToTcpAndWritesTheRenewedEntry` failed on Linux and macOS in CI run 43523918778 (https://github.com/StewartScottRogers/Curl/actions/runs/43523918778). First failing commit: 73f075a3.

    Assertion failed. Expected collection to contain a specific number of elements.

Lanes test only on Windows, so reproduce with `gh run view 43523918778 --log-failed` and fix it platform-neutrally (CLAUDE.md, "Tests pass on Windows, Linux and macOS").

## Acceptance criteria

- [x] `RunAsync_H3EntryForTheOriginAndQuicFails_FallsBackToTcpAndWritesTheRenewedEntry` passes locally, and the `CI` workflow passes on Windows, Linux and macOS for the commit that lands the fix.

## Notes

Root cause: BL-5099 made `AltSvcTransferCache` ignore h3 entries off Windows unless `actsAsQuicBuild`, but nothing passed the flag, so the QUIC-build runner tests (`parsesAsQuicBuild: true`, non-Windows only, never run on Windows) saw no h3 entry. Fix: `TransferPerformer.OpenAltSvcCacheAsync` takes `actsAsQuicBuild`; `CurlCommandRunner` passes `options.ActsAsQuicBuild`. Added `TransferPerformer.Setup.cs` to touches (no other Doing task holds it). The fix also covers BL-5153 to BL-5156 (same tests). Windows cannot run these OSCondition tests; Console and Engine fast tests green, quality run 0 failing.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 94bba3f1.
- 2026-11-13: Doing -> Done. h3 alt-svc entries honoured again for a QUIC-build command line
