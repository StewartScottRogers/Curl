---
id: BL-6065
title: Reuse a library handle's DoH connections from one perform to the next
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs, Curl.Engine.UnitLibrary/KeptDohResolver.cs, Curl.Engine.UnitLibrary/CurlTransports.cs, Curl.Engine.UnitLibrary/EngineComposition.Transports.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Lifecycle.cs, Curl.Engine.UnitLibrary/ManagedEngineSharedState.cs, Curl.Engine.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-26
completed: 2026-11-26
---
# BL-6065 — Reuse a library handle's DoH connections from one perform to the next

## Goal

A library handle's second perform asks the `--doh-url` server over the connection its first perform opened, as libcurl keeps it in the handle's connection cache.

## Context

ADR-1448 (BL-6057): each perform of `ManagedEasyTransfer` builds new transports, so a new `DohDnsResolver`, and disposes it when the perform ends. Reusing a connection needs a resolver kept by the handle (or its share or multi handle) while the DoH settings are unchanged, disposed with that owner. Start at `ManagedCurlEngine.CreateTransports`. Measure libcurl's reuse before pinning it.

## Acceptance criteria

- [x] A unit test shows that two performs of one handle with the same `CURLOPT_DOH_URL`, for two host names, open no new DoH connection in the second perform.
- [x] A unit test shows that disposing the handle closes the kept DoH connections.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- ADR-1453: each library handle owns a KeptDohResolver; ManagedCurlEngine.CreateTransports takes the --doh-url resolver from it, replacing the per-perform disposal of ADR-1448. Kept while the TLS options and the network options but for URL, CURLU and port are unchanged; closed by DisposeAsync.
- Chosen as the simplest scope: per handle, also inside a multi or share handle. Disposing only the engine leaves a handles DoH connections open.
- Reuse not measured with a libcurl program (no libcurl development files here); it rests on ADR-1441s tool measurement and lib/doh.c adding its sub-transfers to the handles multi (ADR-1453).
- touches widened to the six further Engine files above; no other task in Doing on origin/work/dark-factory named them.
- Checks: dotnet build 0 warnings; all fast tests pass; Measure-CodeQuality -Library Curl.Engine.UnitLibrary: 0 failing members.

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: b2981b10 428483f8 3d836d5e.
- 2026-11-26: Doing -> Done. A library handle's later performs reuse its --doh-url connections, which disposing the handle closes
