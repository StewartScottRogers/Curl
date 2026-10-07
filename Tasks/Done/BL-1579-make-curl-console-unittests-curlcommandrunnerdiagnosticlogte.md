---
id: BL-1579
title: Make Curl.Console.UnitTests' CurlCommandRunnerDiagnosticLogTests to CurlCommandRunnerExtendedAttributesTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1579 — Make Curl.Console.UnitTests' CurlCommandRunnerDiagnosticLogTests to CurlCommandRunnerExtendedAttributesTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (9 files, 97 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCommandRunnerDiagnosticLogTests.Authentication.cs`, `CurlCommandRunnerDiagnosticLogTests.cs`, `CurlCommandRunnerDisallowUsernameInUrlTests.cs`, `CurlCommandRunnerDnsFilterRouteTraceTests.cs`, `CurlCommandRunnerDumpHeaderTests.cs`, `CurlCommandRunnerEchModeTests.cs`, `CurlCommandRunnerEndPointTests.cs`, `CurlCommandRunnerEtagTests.cs`, `CurlCommandRunnerExtendedAttributesTests.cs`.

## Context

- Split from BL-1461 (one task per range of files, as its Notes direct); BL-1461 keeps the whole-project checks and depends on this task. Follow BL-1461's Context: what matters in this project (command line, the scripted connector's script, request bytes, stdout and stderr, exit code), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1541 and BL-1542 show the pattern in `Curl.Networking.UnitTests`.
- `CurlCommandRunnerDiagnosticLogTests.cs` already writes output of its own; move it onto the helper's format (BL-1461's Context).
- "These classes' filter" below is one `FullyQualifiedName~Curl.Console.<Class>.` term per class the listed files declare (a partial class's files count once), joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Console.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Console.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Pattern of BL-1578 (commit 8bde5dd42): each class gets `TestContext` and a `Diagnostics` property; its run helper writes ARRANGE command line (and scripted responses or other inputs), a `PHASE run`, ACT exit code and BYTES stdout/stderr (and request bytes where a scripted connector records them); each test mirrors its asserts as ASSERT/DIFF lines. Text built from `Environment.NewLine` is printed with LF. `CurlCommandRunnerDiagnosticLogTests` no longer writes the absolute temp directory: `TestContext.WriteLine(directory)` became `ARRANGE temporary directory: <name only>`, and rooted log-file arguments print as their file name.
- Counts in the nine files, before -> after: `Assert.` 225 -> 225, `[TestMethod` 97 -> 97, `[DataRow(` 41 -> 41 (the new lines are `Diagnostics.Assert(`, which the `Assert.` pattern does not match).
- The classes filter ran 127 tests, all passed, 127 `END` lines, none with arrange 0, act 0 or assert 0.
- No test printed a `SLOW:` line.
- `dotnet build Curl.Console.UnitTests -warnaserror` clean; fast tests 2661 passed, 24 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 97 tests in the nine CurlCommandRunner files write ARRANGE, ACT and ASSERT diagnostics through TestDiagnostics
