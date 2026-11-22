---
id: BL-5770
title: Skip the read-callback pre-read for a scheme no extension handler serves so htp:// never runs the callback
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.Mqtt.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5770 — Skip the read-callback pre-read for a scheme no extension handler serves so htp:// never runs the callback

## Goal

A read-callback POST to a scheme no handler serves (`htp://`) ends with exit 1 and never runs the callback.

## Context

Split from BL-5765. `IsExtensionUrl` is syntactic; the engine learns the served schemes from the dispatcher (`ProtocolDispatcher.Serves`).

## Acceptance criteria

- [x] `htp://` with a read callback fails with `UnsupportedProtocol` and the callback is never called; `nats://`, `mcp://` keep their pre-read body (tests in `ManagedEasyTransferTests.Mqtt.cs`).

## Notes

- `TransferSettingsOf` takes an optional `isServedScheme`; `PerformFromOptionsOverCachesAsync` passes the dispatcher's `Serves` (built only when a pre-read is considered). `null` keeps the old behaviour. Added `ManagedEasyTransfer.Perform.cs` to `touches`; `ManagedCurlEngine.cs` needed no change. Engine quality gates pass; fast tests green.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: fbecace0.
