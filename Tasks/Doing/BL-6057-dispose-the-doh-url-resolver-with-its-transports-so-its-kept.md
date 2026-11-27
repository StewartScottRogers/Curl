---
id: BL-6057
title: Dispose the --doh-url resolver with its transports so its kept connections close
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/CurlTransports.cs, Curl.Engine.UnitLibrary/EngineComposition.Dns.cs, Curl.Engine.UnitTests/EngineCompositionDohTests.cs, Curl.Console/CurlComposition.cs]
requirement: none
created: 2026-11-26
completed:
---
# BL-6057 — Dispose the --doh-url resolver with its transports so its kept connections close

## Goal

Whoever builds the transports disposes the `--doh-url` resolver when their transfers are done, so its kept connections close.

## Context

ADR-1441 (BL-6051): `DohDnsResolver` keeps its DoH connections for later queries and closes them in `DisposeAsync`, which nothing calls, so they stay open until the process ends. That matters most to a long-lived `Curl.Api` or engine host. Start at `EngineComposition.CreateDnsResolver` and `CurlTransports`; follow who owns and disposes the transports.

## Acceptance criteria

- [ ] A unit test shows that disposing the transports (or their owner) closes a connection the DoH resolver kept.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-26: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: c382bbb3 3d836d5e.
