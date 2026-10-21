---
id: BL-2886
title: Give each unit-program port area its own UnitPrograms partial file and its project references, so BL-2842 to BL-2848 run in parallel
priority: High
assignee: Claude
pipeline: direct
depends-on: [BL-2841]
touches: [Curl.Conformance.UnitProgram.UnitLibrary, Curl.Conformance.UnitProgram.UnitTests]
requirement: none
created: 2026-10-21
completed:
---
# BL-2886 — Give each unit-program port area its own UnitPrograms partial file and its project references, so BL-2842 to BL-2848 run in parallel

## Goal

Each of the seven unit-program port tasks (BL-2842 to BL-2848) registers its programs in a file of its own and needs no shared project edit, so all seven can run at once.

## Context

Stewart, 2026-10-21: "always function in a highly parallel fashion". BL-2841 adds `UnitPrograms.Ported`, one list every port task appends to, so the seven ports would run one at a time. Areas and their tasks: Hashes (BL-2842), Asn1 (BL-2843), UrlParsing (BL-2844), ToolParsers (BL-2845), Headers (BL-2846), Dns (BL-2847), Limits (BL-2848).

## Acceptance criteria

- [ ] `UnitPrograms` is a partial class; `Curl.Conformance.UnitProgram.UnitLibrary/UnitPrograms.<Area>.cs` exists for each of the seven areas, each declaring an empty program array (e.g. `private static IUnitProgram[] HashesPrograms => [];`), and `Ported` is built from all seven arrays, so adding a program edits only its area's file.
- [ ] Empty `Curl.Conformance.UnitProgram.UnitLibrary/<Area>/` and `Curl.Conformance.UnitProgram.UnitTests/<Area>/` folders are not needed; the port tasks create them.
- [ ] `Curl.Conformance.UnitProgram.UnitLibrary`'s csproj references every Curl library the seven tasks' Goals say they port against (read each task; e.g. Curl.Cryptography for hashes, the URL, cookie/header, DNS and TLS libraries), so no port task edits the csproj.
- [ ] A test proves `Ported` contains each area's programs (none yet) and still rejects duplicate names.
- [ ] `dotnet build` clean, fast tests green, the project inside the quality gates.

## Notes

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: d8b55c76 6dd7cfce 6157d435 0f33084e 3dbaac52.
