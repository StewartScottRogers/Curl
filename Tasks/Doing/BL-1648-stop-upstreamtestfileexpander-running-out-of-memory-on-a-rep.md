---
id: BL-1648
title: Stop UpstreamTestFileExpander running out of memory on a %repeat count near int.MaxValue
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1648 — Stop UpstreamTestFileExpander running out of memory on a %repeat count near int.MaxValue

## Goal

`UpstreamTestFileExpander.Expand` answers a `%repeat[N x …]%` whose output would be too large to hold with a named, bounded result (the instruction left as written and listed in `UnsupportedInstructions`, or a documented refusal) instead of an `OutOfMemoryException`.

## Context

- Found by BL-1494 (adversarial black-box tests of `Curl.Conformance.UnitLibrary`), by reading the public path, not by running it: running it would take gigabytes on a machine shared by nine lanes.
- `UpstreamTestInstructions.TryReplaceRepeatAt` (`Curl.Conformance.UnitLibrary/UpstreamTestInstructions.cs`) parses any count up to `int.MaxValue` and builds `string.Concat(Enumerable.Repeat(content, count))`. `Expand("%repeat[2000000000 x ab]%\n"u8, …)` asks for a 4-billion-character string, past the CLR's string limit, so it throws `OutOfMemoryException` after allocating up to that limit; the expander's doc comment promises no exception. Counts past `int.MaxValue` are already left as written (pinned by BL-1494's `Expand_InstructionThatDoesNotMatch_IsLeftAsWritten`).
- Upstream's `testutil.pm` (`subbase64`, curl-8_21_0) has no limit either, but the largest count in the vendored `tests/data` is far below any sensible cap; pick a cap on the output (for example 16 MiB) and record the choice in an ADR.
- The test lands with the fix, using a count whose output is over the cap but whose check runs without allocating it.

## Acceptance criteria

- [ ] A test in `Curl.Conformance.UnitTests` expands `%repeat[2000000000 x ab]%` and gets the chosen bounded answer without allocating the output; it fails (or is impossible to run) before the fix.
- [ ] Every vendored upstream case still expands as before: `UpstreamConformanceTests` passes with `PassingUpstreamCases.txt` unchanged.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes; `Curl.Conformance.UnitLibrary` keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
