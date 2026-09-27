---
id: BL-334
title: Compose one pooling connector per command line in Curl.Console
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-335, BL-215]
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-334 — Compose one pooling connector per command line in Curl.Console

## Goal

`CurlComposition.CreateTransports` wraps its `TcpConnector` in one `PoolingConnector` per command line, hands it to every handler, and disposes it when the run ends, so a second URL to the same key reuses the first URL's connection.

## Context

- ADR-0050, section "A pooling connector owns the pool, one per command line". If the ADR and this task disagree, the ADR wins.
- `PoolingConnector` is built by BL-215 in `Curl.Networking.UnitLibrary`; the contract comes from BL-335.
- Disposing the pool at the end of the run writes nothing (curl's `-v` shows nothing after the last `left intact`).
- `--next`, `-Z`/`--parallel` and `--no-keepalive` are not part of this task.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` (`CurlTransportsTests` or `CurlCompositionTests`) pins that every handler the composition builds receives the same `PoolingConnector` instance.
- [ ] A test pins that the pool is disposed when the run ends, including when a transfer fails.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

## Log

- 2026-09-27: Created.
- 2026-09-27: Backlog -> Doing.
