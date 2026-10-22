---
id: BL-1587
title: Make Curl.Console.UnitTests' CurlCommandRunnerUnimplementedOptionTests to CurlCommandRunnerWriteOutTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1587 — Make Curl.Console.UnitTests' CurlCommandRunnerUnimplementedOptionTests to CurlCommandRunnerWriteOutTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (11 files, 109 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCommandRunnerUnimplementedOptionTests.cs`, `CurlCommandRunnerUploadTests.cs`, `CurlCommandRunnerUrlCredentialsTests.cs`, `CurlCommandRunnerUrlExpansionTests.cs`, `CurlCommandRunnerUrlRejectedVerboseTests.cs`, `CurlCommandRunnerUseAsciiTests.cs`, `CurlCommandRunnerVerboseProgressMeterTests.cs`, `CurlCommandRunnerVerifyResultTests.cs`, `CurlCommandRunnerVersionTests.cs`, `CurlCommandRunnerWarningLineTests.cs`, `CurlCommandRunnerWriteOutTests.cs`.

## Context

- Split from BL-1461 (one task per range of files, as its Notes direct); BL-1461 keeps the whole-project checks and depends on this task. Follow BL-1461's Context: what matters in this project (command line, the scripted connector's script, request bytes, stdout and stderr, exit code), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1541 and BL-1542 show the pattern in `Curl.Networking.UnitTests`.
- "These classes' filter" below is one `FullyQualifiedName~Curl.Console.<Class>.` term per class the listed files declare (a partial class's files count once), joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Console.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Console.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Pattern: BL-1586's (`CurlCommandRunnerTransferWarningTests`). Each class's shared run helper writes ARRANGE (arguments, handler schemes and behaviour, inputs), runs inside `PHASE run`, then ACT (exit code, LF-normalised stderr) and stdout as `BYTES`; every real `Assert.` has a `Diagnostics.Assert` or `Diff` before it.
- OS-neutral output: text is printed with `\r\n` shown as `\n`; CRLF pinned by `runsOnWindows: true` runs is compared exactly (it comes from the flag, not the machine); `--version` tests print only OS-independent facts (stdout written, stderr length, a true/false equality ASSERT).
- `CurlCommandRunnerUnimplementedOptionTests` would end at `Assert.Inconclusive` with zero act/assert counts only if no unimplemented option remained; today both runs print arrange 4, act 2, assert 4.
- Counts in the 11 files, before -> after: `Assert.` 300 -> 300, `[TestMethod` 109 -> 109, `[DataRow(` 46 -> 46 (the added lines are `Diagnostics.Assert(`, which the `Assert\.` pattern does not match).
- Filter run: 137 tests, 137 `END` lines, none with a zero count; all passed. Fast tests of `Curl.Console.UnitTests`: 2661 passed, 24 skipped, 0 failed.
- No test printed a `SLOW:` line.
## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 109 tests in CurlCommandRunnerUnimplementedOptionTests..CurlCommandRunnerWriteOutTests write ARRANGE/ACT/ASSERT diagnostics
