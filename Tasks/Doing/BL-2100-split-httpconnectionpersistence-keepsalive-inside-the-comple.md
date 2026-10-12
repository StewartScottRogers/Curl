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
completed:
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

- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports no member outside a gate.
- [ ] Behaviour is unchanged: the existing `KeepsAlive` tests pass unmodified.
- [ ] `dotnet build -warnaserror is clean and the fast tests (dotnet test --filter "TestCategory!=Integration") are green.

## Notes

## Log

- 2026-10-11: Created.
- 2026-10-11: Backlog -> Doing. interactive fix
