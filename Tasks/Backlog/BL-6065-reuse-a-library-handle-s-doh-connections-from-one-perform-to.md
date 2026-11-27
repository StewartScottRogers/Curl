---
id: BL-6065
title: Reuse a library handle's DoH connections from one perform to the next
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedCurlEngine.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Perform.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6065 — Reuse a library handle's DoH connections from one perform to the next

## Goal

A library handle's second perform asks the `--doh-url` server over the connection its first perform opened, as libcurl keeps it in the handle's connection cache.

## Context

ADR-1448 (BL-6057): each perform of `ManagedEasyTransfer` builds new transports, so a new `DohDnsResolver`, and disposes it when the perform ends. Reusing a connection needs a resolver kept by the handle (or its share or multi handle) while the DoH settings are unchanged, disposed with that owner. Start at `ManagedCurlEngine.CreateTransports`. Measure libcurl's reuse before pinning it.

## Acceptance criteria

- [ ] A unit test shows that two performs of one handle with the same `CURLOPT_DOH_URL`, for two host names, open no new DoH connection in the second perform.
- [ ] A unit test shows that disposing the handle closes the kept DoH connections.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
