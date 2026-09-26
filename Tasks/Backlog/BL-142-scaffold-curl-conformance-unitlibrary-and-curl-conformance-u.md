---
id: BL-142
title: Scaffold Curl.Conformance.UnitLibrary and Curl.Conformance.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-005]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.slnx]
requirement: none
created: 2026-09-26
completed:
---
# BL-142 — Scaffold Curl.Conformance.UnitLibrary and Curl.Conformance.UnitTests

## Goal

The solution has an empty `Curl.Conformance.UnitLibrary` and its `Curl.Conformance.UnitTests`
twin, building and tested, ready to hold the upstream conformance harness.

## Context

- ADR-0013 (`Documentation/Planning/Decisions/ADR-0013-upstream-test-cases-run-as-data-driven-mstest.md`),
  decision 2: the harness lives in these two projects.
- Use the `new-project` skill; it knows the naming, the flat layout and the `Curl.slnx` ordering.
- The library references `Curl.Protocol.Abstractions.UnitLibrary` (it implements `IConnector`
  and `IDatagramConnector` for the in-memory servers). The test project references the library
  and `Curl.Console` (the runner, BL-147).
- Each project gets a `CLAUDE.md` saying what it holds and that it follows ADR-0013.

## Acceptance criteria

- [ ] `Curl.Conformance.UnitLibrary/` and `Curl.Conformance.UnitTests/` exist at the repository
      root and are listed in `Curl.slnx` in the flat alphabetical run, the tests directly after
      the library.
- [ ] Neither project file has a `Version` on a `PackageReference`, and no package is added.
- [ ] The test project holds at least one passing test.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-09-26: Created.
