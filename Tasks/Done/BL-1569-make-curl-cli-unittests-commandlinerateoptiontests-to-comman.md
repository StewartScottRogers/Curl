---
id: BL-1569
title: Make Curl.Cli.UnitTests' CommandLineRateOptionTests to CommandLineRefusalTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1569 — Make Curl.Cli.UnitTests' CommandLineRateOptionTests to CommandLineRefusalTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineRateOptionTests.cs`, `CommandLineRedirectAndFailOptionTests.cs`, `CommandLineRefusalTests.cs` (80 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineRateOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineRedirectAndFailOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineRefusalTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- `Assert.` / `[TestMethod` / `[DataRow(` matches, before -> after (2026-10-07): `CommandLineRateOptionTests.cs` 8/13/54 -> 8/13/54; `CommandLineRedirectAndFailOptionTests.cs` 86/50/82 -> 86/50/82; `CommandLineRefusalTests.cs` 17/17/5 -> 17/17/5. No test's logic or assertion changed.
- The acceptance filter ran 193 tests (190 passed, 3 skipped by `OSCondition` on Windows) and printed 190 `END` lines, none with a zero arrange, act or assert count.
- No test printed a `SLOW:` line; the whole filtered run took 1.3 s.
- Helpers chosen: the parser tests route through a private `Parse` that writes `ARRANGE arguments` (each quoted) and `ActParse`'s lines, as BL-1568 did; `CommandLineRedirectAndFailOptionTests` also writes every redirect and fail option as `ACT` lines for an accepted parse, and its `Accept`, `AssertRefused` and `AssertWarnings` write the `ASSERT` lines. `CommandLineRefusalTests` builds refusals directly, so each test writes the factory call it makes as `ARRANGE call`, the refusal's exit code and stderr lines as `ACT`, and the expected exit code and lines as `ASSERT`.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All three files write ARRANGE, ACT and ASSERT diagnostics; build clean, fast tests green
