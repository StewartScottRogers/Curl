---
id: BL-6066
title: Share a library handle's kept DoH connections through its multi and share handles
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-6065]
touches: [Curl.Engine.UnitLibrary/KeptDohResolver.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs, Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/ManagedMultiTransfer.cs, Curl.Engine.UnitLibrary/ManagedShareHandle.cs, Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/CLAUDE.md, Curl.Engine.UnitTests/EngineCompositionDohTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6066 — Share a library handle's kept DoH connections through its multi and share handles

## Goal

Library handles in one multi handle, or sharing connections through a share handle, reuse each others `--doh-url` connections, as libcurls shared connection cache lets them.

## Context

ADR-1449 (BL-6065): each `ManagedEasyTransfer` owns its `KeptDohResolver`, also inside a multi or share handle, and disposing only the engine leaves its connections open. Start at `ManagedEasyTransfer.PerformShared` and `ManagedEngineSharedState`, where the connection cache is chosen.

## Acceptance criteria

- [ ] A unit test shows that a second handle in the same multi handle, with the same `CURLOPT_DOH_URL`, opens no new DoH connection.
- [ ] A unit test shows that a second handle sharing connections through a share handle opens no new DoH connection.
- [ ] A unit test shows that cleaning up the owner of the connection cache closes the kept DoH connections.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
