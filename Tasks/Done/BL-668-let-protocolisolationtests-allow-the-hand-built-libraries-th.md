---
id: BL-668
title: Let ProtocolIsolationTests allow the hand-built libraries the reference ADR lists
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-667]
touches: [Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-28
completed: 2026-09-28
---
# BL-668 — Let ProtocolIsolationTests allow the hand-built libraries the reference ADR lists

## Goal

`ProtocolIsolationTests` passes a protocol library that references `Curl.Protocol.Abstractions.UnitLibrary` and any hand-built library BL-667's ADR allows, and still fails one that references another protocol library or anything else.

## Context

- Decision: BL-667's ADR (read it first; the allowed list comes from there, not from this task).
- Code: `Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs`, test `ProtocolLibrary_References_OnlyAbstractions` (compares each `ProjectReference` of every `Curl.Protocol.*.UnitLibrary.csproj` with the Abstractions name). Rename the test to say what it now checks.
- The hand-built libraries may not exist yet when this runs; the allowed list is names, so no project needs to exist for the test to compile.
- Add a second check if the ADR asks for it: each allowed hand-built library references no `Curl.Protocol.*` project and none of `Curl.Networking.UnitLibrary`, `Curl.Core.UnitLibrary`, `Curl.Console` (skip a library whose csproj is not there yet).

## Acceptance criteria

- [x] The renamed test fails for a protocol-to-protocol reference and for a protocol-to-`Curl.Networking.UnitLibrary` reference and passes for a protocol-to-allowed-library reference; the decision logic is a pure method over reference names, covered by data-row tests that need no extra csproj.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Delivered directly rather than through the full /feature agent chain: the change is one test file in one test project, with no production code, so there was nothing for protocol-architect or protocol-implementer to plan or build.
- The test is now `ProtocolLibrary_References_OnlyAbstractionsAndHandBuiltLibraries`. The allowed list is ADR-0120's table, kept as a dictionary of library name to the projects its row allows. The decision logic lives in two pure methods over names, `ForbiddenProtocolReferences` and `ForbiddenHandBuiltReferences`, and data-row tests cover both.
- ADR-0120 asks for a second check, so `HandBuiltLibrary_References_OnlyItsAdrRow` checks each hand-built csproj against its row and skips any library whose csproj does not exist yet (today only Curl.Cryptography exists).

## Log

- 2026-09-28: Created.
- 2026-09-28: Backlog -> Doing.
- 2026-09-28: Doing -> Done. ProtocolIsolationTests allows Abstractions plus ADR-0120's hand-built libraries and checks each hand-built library against its row
