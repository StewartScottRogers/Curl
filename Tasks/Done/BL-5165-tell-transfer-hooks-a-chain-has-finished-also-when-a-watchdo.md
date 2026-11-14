---
id: BL-5165
title: Tell transfer hooks a chain has finished also when a watchdog or a hook exception ends it
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs, Curl.Engine.UnitLibrary/TransferPerformer.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferHookingProtocolHandler.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/ITransferHook.cs, Curl.Protocol.Abstractions.UnitLibrary/IProtocolHandler.cs, Curl.Core.UnitTests/RedirectFollowerTests.cs, Curl.Engine.UnitTests, Curl.Extension.Abstractions.UnitTests]
requirement: none
created: 2026-11-13
completed: 2026-11-13
---
# BL-5165 — Tell transfer hooks a chain has finished also when a watchdog or a hook exception ends it

## Goal

A transfer hook's `TransferChainFinishedAsync` is called for every transfer, also one a `-m` or `-Y`/`-y` watchdog stopped with exit 28, and a hook that throws there neither stops the hooks after it nor replaces the transfer's finished result.

## Context

Found by the shift-end review of 2026-11-13 (ADR-0710) - silent-failure-hunter, type-design-analyzer and code-reviewer all reported it - in what BL-5164 merged (commit c13a43141).

- `Curl.Core.UnitLibrary/RedirectFollower.cs:194-204`: `FollowAsync` calls `dispatcher.NotifyChainFinishedAsync` only after `FollowChainAsync`/`DispatchAsync` return. When the `-m` (`MaxTimeWatchdog`) or `-Y`/`-y` (`LowSpeedWatchdog`) watchdog cancels the transfer, the `OperationCanceledException` leaves `FollowAsync` before the notify, and `TransferPerformer.FollowWatchingSpeedAsync` (`Curl.Engine.UnitLibrary/TransferPerformer.cs:182-193`) turns it into the exit 28 result outside the follower. No hook is told. That breaks the doc on `ITransferHook.TransferChainFinishedAsync` ("however it ended") and on `IProtocolHandler.TransferChainFinishedAsync` ("Called once per transfer"), and the OpenTelemetry hook BL-5164 was built for (BL-5162) would export no span for a timed-out transfer. A plain `finally` is not enough: no result exists yet when the exception passes through the follower.
- `Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferHookingProtocolHandler.cs:40-46`: an exception from one hook's `TransferChainFinishedAsync` stops the loop, so later hooks are not told, and escapes `FollowAsync` after the result is final, so a finished (even successful) transfer turns into an exception instead of its exit code.
- `--retry`: `FollowWatchingSpeedAsync` calls `FollowAsync` once per attempt, so hooks are told once per attempt. Each attempt is its own transfer in curl (its own `%{xfer_id}`), so per attempt is right, but the docs' "once" must say so.

## Acceptance criteria

- [x] A transfer stopped by the `-m` watchdog, and one stopped by the `-Y`/`-y` watchdog, each call `TransferChainFinishedAsync` exactly once with the exit 28 result; unit tests show both.
- [x] When one hook's `TransferChainFinishedAsync` throws, every other hook is still told and the transfer's exit code is the one the chain produced; a unit test in `Curl.Extension.Abstractions.UnitTests` shows it, and the `ITransferHook` doc comment states the contract.
- [x] The doc comments on `ITransferHook.TransferChainFinishedAsync` and `IProtocolHandler.TransferChainFinishedAsync` say exactly when it is called, including that each `--retry` attempt is told on its own.
- [x] `dotnet build` clean, fast tests green, the touched libraries at 100% line and branch coverage with no member over the complexity or CRAP gates.

## Notes

- Watchdog exit 28: `TransferPerformer.FollowWatchingSpeedAsync` now tells the handler through the new public `RedirectFollower.NotifyChainFinishedAsync`. A hook exception in `TransferHookingProtocolHandler.TransferChainFinishedAsync` is swallowed per hook. Docs state per-attempt under `--retry`. CI on Linux and macOS: change is platform-neutral. Quality run: 0 failing members.

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: af42ca3b 638c5cf6 2ee82dc8 225a7ab7.
- 2026-11-13: Doing -> Done. done
