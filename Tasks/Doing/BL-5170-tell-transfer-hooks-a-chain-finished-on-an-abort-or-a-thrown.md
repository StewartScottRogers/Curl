---
id: BL-5170
title: Tell transfer hooks a chain finished on an abort or a thrown hop too so OpenTelemetry chain state is not leaked
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Engine.UnitTests/TransferPerformerTests.cs, Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/ITransferHook.cs, Curl.Protocol.Abstractions.UnitLibrary/IProtocolHandler.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5170 — Tell transfer hooks a chain finished on an abort or a thrown hop too so OpenTelemetry chain state is not leaked

## Goal

Every way a transfer leaves RedirectFollower.FollowAsync - a result, a watchdog, an abort token or an exception from a hop - tells the handler (and so its hooks) once that the chain finished, as IProtocolHandler and ITransferHook document, so OpenTelemetryTransferHook never keeps a stale chain entry.

## Context

Found by the shift-end review of 7747c1d4d...8f429f2ab (type-design-analyzer; silent-failure-hunter raised it as a suggestion). ITransferHook.TransferChainFinishedAsync (Curl.Extension.Abstractions.UnitLibrary/Transfer/ITransferHook.cs:77-84) and IProtocolHandler (Curl.Protocol.Abstractions.UnitLibrary/IProtocolHandler.cs:26-31) say the hook is told once per transfer, however it ended. After BL-5165 only a result and the two watchdog catches in TransferPerformer.FollowWatchingSpeedAsync (Curl.Engine.UnitLibrary/TransferPerformer.cs:186-193) call NotifyChainFinishedAsync; an abort through the abort token (TransferContextFactory.cs abortToken, used by Libcurl and Curl.Api hosts) or an exception from a hop leaves FollowAsync with nobody telling the hooks. BL-5162 moved OpenTelemetryTransferHook's chain state into a ConcurrentDictionary (_chains, OpenTelemetryTransferHook.cs:26,46): an entry stored after a followed 3xx is removed only by the next hop or TransferChainFinishedAsync, so each aborted followed chain leaks one entry holding its last ITransferContext and TransferHookServices for the hook's lifetime in a long-lived Curl.Api/Libcurl host, and its span is never exported. Either notify on every exit path (a finally-style path in FollowWatchingSpeedAsync or RedirectFollower) with a result matching the exit (for example the abort's own failure), or narrow both doc comments to what is delivered and have the hook drop the entry itself.

## Acceptance criteria

- [x] An aborted transfer (abort token cancelled, no watchdog fired) that had followed a 3xx leaves no entry in OpenTelemetryTransferHook's chain state, shown by a unit test.
- [x] The doc comments of ITransferHook.TransferChainFinishedAsync and IProtocolHandler.NotifyChainFinishedAsync state exactly which endings are told, and a test pins each told ending.
- [x] The aborted transfer's exception and exit code are unchanged.
- [x] dotnet build is clean and the fast tests pass; Curl.Engine.UnitLibrary and Curl.Core.UnitLibrary keep 100% line and branch coverage.

## Notes

- FollowWatchingSpeedAsync now tells the handler on any other exception too: exit 42 for a cancellation, exit 2 otherwise, then rethrows the same exception (exception and exit code unchanged). Doc comments of ITransferHook and IProtocolHandler list the told endings (result, refused hop, watchdogs, abort token, hop exception). Tests pin the abort and the thrown hop in TransferPerformerTests; the hook drops its entry in TransferChainFinishedAsync, already covered by OpenTelemetryChainEndTests (test project outside touches). Engine library at 100% line and branch, 0 failing members. Platform-neutral.

## Log

- 2026-11-13: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: a04444ea 48cd604d e19db8f2 448f1e8f.
