---
id: BL-2100
title: Split HttpConnectionPersistence.KeepsAlive inside the complexity gate
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
lane: no
requirement: none
created: 2026-10-11
completed: 2026-10-11
---
# BL-2100 — Split HttpConnectionPersistence.KeepsAlive inside the complexity gate

## Goal

`HttpConnectionPersistence.KeepsAlive` measures at most 10 complexity on the coverage page,
so CI's Quality gates job is green again on work/dark-factory.

## Context

CI on e94294e2c failed Quality gates: `KeepsAlive`
(`Curl.Protocol.Http.UnitLibrary/HttpConnectionPersistence.cs:35`) is outside the complexity
gate since 35470e761 (BL-2085) added the HTTP/1.1 proxy branch. The build's CA1502 passed it;
the coverage page counts compiled branches.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no member outside a gate.
- [x] Behaviour is unchanged: the existing `KeepsAlive` tests pass unmodified.
- [x] `dotnet build -warnaserror is clean and the fast tests (dotnet test --filter "TestCategory!=Integration") are green.

## Notes

KeepsAlive, AsksToClose and BodyRunsToClose are all inside the gates (fb77bd1ab). Measured with -Library alone, HttpProtocolHandler.WriteHeadersAsync (BL-2092) also shows 83% branches: its no-line-feed side (line 2767) is reached only by other test projects, and CI, which measures them all together, passed it.

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing. interactive fix
- 2026-10-11: Doing -> Done. KeepsAlive split inside the gate (fb77bd1ab)
