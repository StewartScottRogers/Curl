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
completed:
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

- [ ] The renamed test fails for a protocol-to-protocol reference and for a protocol-to-`Curl.Networking.UnitLibrary` reference and passes for a protocol-to-allowed-library reference; the decision logic is a pure method over reference names, covered by data-row tests that need no extra csproj.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-28: Created.
