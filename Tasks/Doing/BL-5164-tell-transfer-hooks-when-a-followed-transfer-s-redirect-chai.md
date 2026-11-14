---
id: BL-5164
title: Tell transfer hooks when a followed transfer's redirect chain has ended
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer/ITransferHook.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferHookingProtocolHandler.cs, Curl.Extension.Abstractions.UnitTests, Curl.Console/CurlCommandRunner.cs]
requirement: none
created: 2026-11-13
completed:
---
# BL-5164 — Tell transfer hooks when a followed transfer's redirect chain has ended

## Goal

A transfer hook is told once, with the final result, when a transfer's whole redirect chain (or single request) has ended, however it ended.

## Context

Found by BL-5162. `RedirectFollower` runs the hook chain once per hop, so `ITransferHook.ExecuteAsync` and `TransferFinished` see each hop but never the end: when `RedirectFollower.StopBeforeHop` refuses a hop the hook is never called again. The OpenTelemetry hook needs that end to export its one span (BL-5162 depends on this). Add a default-implemented `ITransferHook` member (for example `TransferChainFinishedAsync(ITransferContext first, TransferResult result)`) and have the host call it after the follower returns, once per transfer.

## Acceptance criteria

- [ ] A hook is told once per transfer, after the last hop, with the final merged result, also when the follower refused a hop.
- [ ] Hooks that do not implement it behave as before; unit tests cover the call and the refused-hop case; build clean, fast tests green, 100% coverage kept.

## Notes

## Log

- 2026-11-13: Created.
- 2026-11-13: Backlog -> Doing. Criteria at claim: 8248b0ba 3a0ed95e.
