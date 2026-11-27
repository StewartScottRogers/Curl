---
id: BL-6067
title: Share a library handle's kept DoH connections through its multi and share handles
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6065]
touches: [Curl.Engine.UnitLibrary/KeptDohResolver.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/ManagedShareHandle.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/EngineCompositionDohTests.cs]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6067 — Share a library handle's kept DoH connections through its multi and share handles

## Goal

Library handles in one multi handle, or sharing connections through a share handle, reuse each others `--doh-url` connections, as libcurls shared connection cache lets them.

## Context

ADR-1453 (BL-6065): each `ManagedEasyTransfer` owns its `KeptDohResolver`, also inside a multi or share handle, and disposing only the engine leaves its connections open. Start at `ManagedEasyTransfer.PerformShared` and `ManagedEngineSharedState`, where the connection cache is chosen.

## Acceptance criteria

- [x] A unit test shows that a second handle in the same multi handle, with the same `CURLOPT_DOH_URL`, opens no new DoH connection.
- [x] A unit test shows that a second handle sharing connections through a share handle opens no new DoH connection.
- [x] A unit test shows that cleaning up the owner of the connection cache closes the kept DoH connections.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Design (ADR-1454): `KeptDohResolver` now sits beside each connection cache
  (`ManagedEngineSharedState.DohResolver`), not on each easy handle. It keeps one resolver per set of
  settings, at most four, so transfers with different settings over one cache do not close each
  other's connections; ADR-1453's close-on-replace at the end of a perform is gone, so its test now
  pins that earlier connections stay.
- `ManagedEasyTransfer.Perform.cs` added to `touches`: its wrapper that closed replaced resolvers
  went. No other task in Doing on `origin/work/dark-factory` named it.
- Tests in `EngineCompositionDohTests`: `PerformAsync_ByASecondHandleInTheSameMulti_...`,
  `PerformAsync_ByASecondHandleSharingConnections_...`, `Multi_CleanedUpOrToldToClearConnections_...`,
  `Cleanup_OfAShareHandle_...`, `DisposeAsync_OfTheEngine_...`.
- Fast tests: all 115 assemblies passed. `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`:
  0 failing members. In-memory fakes only, so platform-neutral.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 70afa1aa e5b0c33b 11e1bcc7 3d836d5e.
- 2026-11-26: Doing -> Done. Library handles in one multi handle, or sharing connections, reuse each other's DoH connections
