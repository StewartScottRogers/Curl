---
id: BL-1573
title: Make Curl.Cli.UnitTests' CommandLineTraceOptionTests to CommandLineUploadFileOptionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1573 — Make Curl.Cli.UnitTests' CommandLineTraceOptionTests to CommandLineUploadFileOptionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineTraceOptionTests.cs`, `CommandLineTransferEncodingOptionTests.cs`, `CommandLineUnimplementedOptionTests.cs`, `CommandLineUnixSocketOptionTests.cs`, `CommandLineUploadFileOptionTests.cs` (84 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineTraceOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineTransferEncodingOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineUnimplementedOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineUnixSocketOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineUploadFileOptionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Each file got the `TestContext`/`Diagnostics` pair and an instance `Parse` helper that writes `ARRANGE arguments`
  (each quoted, via `CommandLineParseDiagnostics.ArrangeArguments`), `ActParse` (accepted, exit code, every stderr
  and warning line) and the option values its tests check; every test then writes `ASSERT` lines for the values it
  asserts, and the shared `AssertRefused` helpers write `AssertRefusal`. Config-file inputs go out as `BYTES`.
  Calls that went straight to `CommandLineParser.Parse` / `OpenSslBuildParser.Parse` now go through the helper,
  which calls the same overload with the same arguments.
- `CommandLineUnimplementedOptionTests` loops over the derived unimplemented-alias set; each loop test writes the set
  as `ARRANGE`, then `ACT`/`ASSERT aliases checked`, so a test still shows its counts if the set is ever empty.
- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before -> after, unchanged in every file:
  Trace 65/25/86 -> 65/25/86; TransferEncoding 67/40/32 -> 67/40/32; Unimplemented 9/9/5 -> 9/9/5;
  UnixSocket 14/5/17 -> 14/5/17; UploadFile 12/5/2 -> 12/5/2.
- The filtered run printed 196 `END` lines for 196 tests, none with a zero arrange, act or assert count.
  No test printed a `SLOW:` line. `Curl.Cli.UnitTests` fast run: 3770 passed, 16 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in CommandLineTraceOptionTests to CommandLineUploadFileOptionTests writes ARRANGE, ACT and ASSERT diagnostics
