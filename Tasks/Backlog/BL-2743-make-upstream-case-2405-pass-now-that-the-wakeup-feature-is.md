---
id: BL-2743
title: Make upstream case 2405 pass now that the wakeup feature is listed (curl_multi_waitfds, expected exit 0, got 44)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2661]
touches: [Curl.Conformance.LibTest.UnitLibrary, Curl.Conformance.LibTest.UnitTests, Curl.Conformance.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-18
completed:
---
# BL-2743 — Make upstream case 2405 pass now that the wakeup feature is listed (curl_multi_waitfds, expected exit 0, got 44)

## Goal

Upstream case 2405 (lib2405, `curl_multi_waitfds`) passes through the runner and is added to `PassingUpstreamCases.txt`.

## Context

Found by BL-2661, which listed the `wakeup` feature (ADR-0690): 2405 now runs and fails with `<verify><errorcode>: expected exit code 0, got 44` (`Curl.Conformance.LibTest.UnitLibrary/Lib2405.cs`). Run it with `CURL_UPSTREAM_CASE=2405 dotnet test Curl.Conformance.UnitTests --filter "TestCategory=Manual"`. It is not on the ratchet until it passes.

## Acceptance criteria

- [ ] Case 2405 passes and is listed in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] A focused unit test in the fixed library's own `.UnitTests` project covers the fix.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

- 2026-10-18 (lane 2, first claim): ran `CURL_UPSTREAM_CASE=2405`; it fails with exit 44 (`LibTestErrorCodes.Failure`). BL-2750's Notes measured the cause on the same port: `Max number of waitfds: 0 not as expected: 3`. The port (`Lib2405`, `Lib2405Run`) is faithful; both causes are in the engine's `ManagedMultiTransfer`, not in the LibTest library:
  1. The first `PerformAsync` finishes both transfers (the in-memory servers answer synchronously, so each started perform has completed before `MoveTransfersOnAsync` queues the done messages) and answers `running == 0`, so the loop never calls `curl_multi_waitfds` on a started transfer. libcurl's first `curl_multi_perform` never finishes an HTTP transfer (the connect is non-blocking); the engine should not report a transfer it started in this call as done until a later perform.
  2. `GetWaitSockets` gives one socket per started transfer and no wakeup descriptor, while `curl_version_info` reports `AsynchDNS` without c-ares (`ManagedCurlVersion`), so lib2405 expects `2 + 1`. The engine should count one more descriptor while a started transfer runs, matching the threaded resolver's (and leave the empty-multi and added-not-run counts at 0).
- So the fix needs `Curl.Engine.UnitLibrary` and `Curl.Engine.UnitTests` (added to `touches`). BL-2747, in Doing on `origin/work/dark-factory`, touches both, so the task goes back to Backlog until it is Done. No code was changed in this claim. BL-2750 (case 2407) holds a stashed engine fix for `PollWaitSocket` on engine-numbered sockets and depends on this task; check it does not collide.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: 29c71c3e ab396f86 3d836d5e.
- 2026-10-18: Doing -> Backlog. Needs Curl.Engine.UnitLibrary and Curl.Engine.UnitTests (ManagedMultiTransfer perform and waitfds), which BL-2747 in Doing touches
