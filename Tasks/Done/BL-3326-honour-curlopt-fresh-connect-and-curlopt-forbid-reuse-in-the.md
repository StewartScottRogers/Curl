---
id: BL-3326
title: Honour CURLOPT_FRESH_CONNECT and CURLOPT_FORBID_REUSE in the engine's connection pool
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Networking.UnitTests/PoolingConnectorTests.cs, Curl.Engine.UnitLibrary/EngineComposition.Connectors.cs, Curl.Engine.UnitTests/EngineCompositionPoolingConnectorTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3326 — Honour CURLOPT_FRESH_CONNECT and CURLOPT_FORBID_REUSE in the engine's connection pool

## Goal

A handle with CURLOPT_FRESH_CONNECT opens a new connection for its transfer instead of reusing a pooled one, and one with CURLOPT_FORBID_REUSE closes its connection after the transfer, as libcurl 8.22.0 does.

## Context

The engine ignores EasyNetworkOptions.FreshConnect and ForbidReuse. BL-3316 only made CURLINFO_NUM_CONNECTS answer 1 for a successful transfer under them (ManagedEasyTransfer.NumConnectsOf); the connection itself is still reused. The call site is in Curl.Engine (ManagedCurlEngine.PoolOf): extend `touches` when the change lands there.

## Acceptance criteria

- [x] A unit test pins that a fresh-connect transfer opens a new connection although an idle one is pooled, and that a forbid-reuse one is not pooled afterwards.
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- touches extended to Curl.Engine.UnitLibrary/EngineComposition.Connectors.cs and a new engine test (no other Doing task names them). PoolingConnector gained FreshConnect (skips sharing and idle reuse, opens new, still pools it afterwards) and ForbidReuse (bars sharing and closes on dispose, also for a reused idle connection); Over keeps both; CreatePoolingConnector sets them from EasyNetworkOptions. Measure-CodeQuality: 0 failing members.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: ea2c0bb5 db93e725.
- 2026-10-23: Doing -> Done. FreshConnect opens a new connection past a pooled idle one and ForbidReuse closes it after the transfer; the engine pool honours both
