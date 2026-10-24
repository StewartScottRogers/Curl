---
id: BL-3409
title: Interpret upstream test1165.pl in C#: configure.ac and the CURL_DISABLE_ macros in the vendored C sources stay in sync (case 1165)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-3406, BL-3407]
touches: [Curl.Conformance.UnitLibrary/UpstreamTest1165Script.cs, Curl.Conformance.UnitTests/UpstreamPerlCommandScripts.Test1165.cs, Curl.Conformance.UnitTests/UpstreamTest1165ScriptTests.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt, Curl.Conformance.UnitTests/Curl.Conformance.UnitTests.csproj]
requirement: none
created: 2026-10-23
completed:
---
# BL-3409 — Interpret upstream test1165.pl in C#: configure.ac and the CURL_DISABLE_ macros in the vendored C sources stay in sync (case 1165)

## Goal

Case 1165 (test1165.pl) runs in C# and passes.

## Context

<!-- Why this matters, and where to start: requirement IDs, ADRs, projects, files, upstream curl links with the curl version. -->

## Acceptance criteria

- [x] Case 1165 passes through the C# interpretation of test1165.pl and is on PassingUpstreamCases.txt.

## Notes

- The task file was an unfilled template; the spec is its title and ADR-0803. UpstreamTest1165Script interprets test1165.pl over the vendored tree (BL-3407). Its source root is %SRCDIR/.., so the csproj now also copies m4, configure.ac and CMakeLists.txt beside docs/lib/src; csproj added to touches (no other Doing task names it; BL-3414 may need a similar line later).
- Measure-CodeQuality for Curl.Conformance.UnitLibrary: 0 failing members; fast tests green. Platform-neutral: vendored files are byte-for-byte, no OS paths in output beyond the root.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: fbecace0.
