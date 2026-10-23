---
id: BL-3325
title: Honour CURLOPT_FRESH_CONNECT and CURLOPT_FORBID_REUSE in the engine's connection pool
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Networking.UnitLibrary/PoolingConnector.cs, Curl.Networking.UnitTests/PoolingConnectorTests.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3325 — Honour CURLOPT_FRESH_CONNECT and CURLOPT_FORBID_REUSE in the engine's connection pool

## Goal

A handle with CURLOPT_FRESH_CONNECT opens a new connection for its transfer instead of reusing a pooled one, and one with CURLOPT_FORBID_REUSE closes its connection after the transfer, as libcurl 8.22.0 does.

## Context

The engine ignores EasyNetworkOptions.FreshConnect and ForbidReuse. BL-3316 only made CURLINFO_NUM_CONNECTS answer 1 for a successful transfer under them (ManagedEasyTransfer.NumConnectsOf); the connection itself is still reused. The call site is in Curl.Engine (ManagedCurlEngine.PoolOf): extend `touches` when the change lands there.

## Acceptance criteria

- [ ] A unit test pins that a fresh-connect transfer opens a new connection although an idle one is pooled, and that a forbid-reuse one is not pooled afterwards.
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-23: Created.
