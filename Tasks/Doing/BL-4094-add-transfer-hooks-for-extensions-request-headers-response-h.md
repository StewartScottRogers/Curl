---
id: BL-4094
title: Add transfer hooks for extensions: request headers, response headers, body and timings
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4092]
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer, Curl.Extension.Abstractions.UnitTests/Transfer, Curl.Engine.UnitLibrary/EngineComposition.cs, Curl.Engine.UnitTests/EngineCompositionTests.Extensions.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4094 — Add transfer hooks for extensions: request headers, response headers, body and timings

## Goal

An extension activated by its option can add request headers, read response headers, observe or transform the response body, and read the transfer's timings, without curl's behaviour changing when no extension option is given.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Needed by SSE, the HTTP cache, digests, checksums, HAR, OpenTelemetry, record and replay, OAuth and most other option extensions. Find the narrowest existing seam in the engine (`Curl.Engine.UnitLibrary`) and expose it through `ProtocolHandlerServices` or a sibling record, as ADR-0999 did for handlers.

## Acceptance criteria

- [ ] A test extension adds a request header, sees the status and headers, rewrites the body and reads the timings of one fake transfer (engine unit test, no network).
- [ ] With no extension option given, a transfer runs no extension hook (unit test proves the hook list is empty).
- [ ] Hooks run after curl's own processing on the way in and before it on the way out, recorded in an ADR marked "Decided by Claude under Stewart's delegation".
- [ ] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes



## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a58a140f b8a5dc4a 0380c217 e35aff77 3d836d5e.
