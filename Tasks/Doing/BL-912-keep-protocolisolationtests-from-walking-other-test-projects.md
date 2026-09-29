---
id: BL-912
title: Keep ProtocolIsolationTests from walking other test projects' bin folders
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-912 â€” Keep ProtocolIsolationTests from walking other test projects' bin folders

## Goal

`ProtocolIsolationTests` finds the protocol projects without walking any `bin`, `obj` or other deep folder, so it never fails while another test project creates or deletes files there.

## Context

- `Curl.Protocol.Abstractions.UnitTests/ProtocolIsolationTests.cs:195` enumerates `Curl.Protocol.*.UnitLibrary.csproj` with `SearchOption.AllDirectories` from the repository root.
- In the whole-solution fast run on 2026-09-29 (BL-627 Notes) both `ProtocolLibrary_References_OnlyAbstractionsAndHandBuiltLibraries` and `EveryProtocolLibrary_HasAMatchingTestProject` threw `UnauthorizedAccessException` on `Curl.Conformance.UnitTests\bin\Debug\net10.0\log\test1001-<guid>`, a folder the Conformance tests were deleting at the same moment. The project passes alone.
- Projects sit immediately under the repository root (root `CLAUDE.md`, "Repository layout"), so one level is enough: enumerate the root's directories named `Curl.Protocol.*.UnitLibrary` and take the `.csproj` inside each.

## Acceptance criteria

- [ ] `ProtocolIsolationTests` no longer uses `SearchOption.AllDirectories`; it looks one level under the repository root.
- [ ] Both tests still find every `Curl.Protocol.*.UnitLibrary` project (the count is unchanged) and pass.
- [ ] `dotnet test Curl.slnx --filter "TestCategory!=Integration"` passes with no `UnauthorizedAccessException` from `ProtocolIsolationTests`.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
