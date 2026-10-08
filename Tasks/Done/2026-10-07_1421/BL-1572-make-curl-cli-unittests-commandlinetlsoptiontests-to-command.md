---
id: BL-1572
title: Make Curl.Cli.UnitTests' CommandLineTlsOptionTests to CommandLineTraceConfigTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1572 — Make Curl.Cli.UnitTests' CommandLineTlsOptionTests to CommandLineTraceConfigTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineTlsOptionTests.cs`, `CommandLineTraceConfigTests.cs` (80 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineTlsOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineTraceConfigTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Filtered run: 179 tests (data rows included), 179 `END` lines, none with a zero count, 0 `SLOW:` lines. No test has phases, so no `PHASE` lines.
- Counts before -> after (`grep -c`): `Assert.` 132 -> 132 and 20 -> 20; `[TestMethod` 68 -> 68 and 12 -> 12; `[DataRow(` 69 -> 69 and 52 -> 52 (CommandLineTlsOptionTests, CommandLineTraceConfigTests).
- Each file got a private `Parse` wrapper that writes `ARRANGE arguments` (quoted), the path check used, `ActParse` and, when accepted, the option values the file pins; refusals assert through `AssertRefusal`, warnings through a quoted `warning lines` assert. Trace component sets are printed sorted ordinally, because a `HashSet<string>`'s order changes with per-process string hashing.
- `Parse_NoTraceConfig_TurnsOnNoComponent` and `Parse_TraceConfigAll_TurnsOnEveryComponentUntilMinusAll` now read the value into a local before asserting it, so the same value is written and asserted; the assertions are unchanged.
- The two `Integration` tests in `CommandLineTlsOptionTests` print the real assembly path they parse; that path is the test's input and is machine-specific by nature.
- Full `Curl.Cli.UnitTests` fast run: 3770 passed, 16 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Both files write descriptive diagnostics; 179 tests print non-zero END counts
