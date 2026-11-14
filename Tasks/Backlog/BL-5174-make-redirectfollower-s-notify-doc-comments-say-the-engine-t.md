---
id: BL-5174
title: Make RedirectFollower's notify doc comments say the engine tells the chain end itself
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Core.UnitLibrary/RedirectFollower.cs]
requirement: none
created: 2026-11-14
completed:
---
# BL-5174 — Make RedirectFollower's notify doc comments say the engine tells the chain end itself

## Goal

`RedirectFollower`'s doc comments say truthfully who tells the handler that the chain ended now that the engine follows through `FollowWithoutNotifyingAsync`.

## Context

The shift-end review (code-reviewer, 2026-11-14) of BL-5172/BL-5173 found that `Curl.Core.UnitLibrary/RedirectFollower.cs:219-223` (the summary of `NotifyChainFinishedAsync`) still says "`FollowAsync` does it when the chain ends; the caller does it for a transfer a watchdog cancelled". Since BL-5172, the engine's only production path (`Curl.Engine.UnitLibrary/TransferPerformer.cs`, `FollowWatchingSpeedAsync`) calls `FollowWithoutNotifyingAsync` and tells the handler itself on every ending: success, watchdog and a hop's exception. No production code calls `FollowAsync` any more; only tests do. CLAUDE.md's "say what it does, do what it says" rule makes the stale comment a defect.

## Acceptance criteria

- [ ] The `NotifyChainFinishedAsync` summary in `RedirectFollower.cs` says that `FollowAsync` tells the handler itself, and that a caller of `FollowWithoutNotifyingAsync` (the engine's `TransferPerformer.FollowWatchingSpeedAsync`) tells it on every ending: success, watchdog exit 28 and a hop's exception.
- [ ] The `FollowAsync` summary says that the engine does not call it, or `FollowAsync` is removed and its tests are moved to `FollowWithoutNotifyingAsync` plus `NotifyChainFinishedAsync`.
- [ ] No doc comment in `RedirectFollower.cs` still says that the watchdog case is the only one the caller handles.
- [ ] `dotnet build` is clean and the fast tests of `Curl.Core.UnitTests` pass.

## Notes

## Log

- 2026-11-14: Created.
