---
id: BL-148
title: Scaffold Curl.Conformance.UnitLibrary and Curl.Conformance.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-005]
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests, Curl.slnx]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-148 — Scaffold Curl.Conformance.UnitLibrary and Curl.Conformance.UnitTests

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

- [x] `Curl.Conformance.UnitLibrary/` and `Curl.Conformance.UnitTests/` exist at the repository
      root and are listed in `Curl.slnx` in the flat alphabetical run, the tests directly after
      the library.
- [x] Neither project file has a `Version` on a `PackageReference`, and no package is added.
- [x] The test project holds at least one passing test.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Choice: the library holds no type yet. An empty scaffold is what the Goal asks for, and
  a placeholder type would be a name that says nothing true. The test project instead
  holds `HarnessReferencesTests` (2 tests), which pin that `Curl.Conformance.UnitLibrary`
  and `curl` (`Curl.Console`) are both copied beside the tests - the two assemblies
  ADR-0013 decisions 2 and 4 say the harness is built from.
- `InternalsVisibleTo Curl.Conformance.UnitTests` in `Curl.Console` (ADR-0013 decision 4)
  is not added here: `Curl.Console` is outside this task's `touches`; it belongs to the
  runner task (BL-147).
- Verified 2026-09-26: `dotnet build` 0 warnings 0 errors; fast tests green in all
  projects (Curl.Conformance.UnitTests 2/2); `dotnet format --verify-no-changes` clean on
  the new test project.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. Curl.Conformance.UnitLibrary and Curl.Conformance.UnitTests exist, are in Curl.slnx, build clean and their 2 tests pass
