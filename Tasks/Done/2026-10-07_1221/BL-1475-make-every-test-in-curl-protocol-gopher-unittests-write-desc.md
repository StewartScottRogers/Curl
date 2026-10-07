---
id: BL-1475
title: Make every test in Curl.Protocol.Gopher.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Gopher.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1475 — Make every test in Curl.Protocol.Gopher.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Protocol.Gopher.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Protocol.Gopher.UnitTests`, which tests `Curl.Protocol.Gopher.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 74 test methods in 5 files, with 22 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the URL, the selector sent (through `Fakes/FakeConnector.cs`), the scripted reply, the bytes written to output and the `CurlExitCode` with its error text.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Gopher.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Protocol.Gopher.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Gopher.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Gopher.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Gopher.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Sized for one run: 74 test methods in 5 files.
- Done 2026-10-07. Each test class has a `Diagnostics` property (`TestDiagnostics.For(TestContext)`). Every test writes ARRANGE lines (the URL, scripted reads, connect result or failing write/read), ACT lines (the `TransferResult` - exit code by name and number, error text, bytes, refused - plus the selector sent, output bytes, `-v` transcript, progress reports or diagnostic-log lines) and ASSERT or DIFF lines for what it checks. Shared helpers write the common lines for the tests that call them: `AssertSelectorSent` (now an instance method), `RunAsync`, `Report` and `AssertTranscript`. Each test's `END` line counts those lines.
- New `DiagnosticText.cs`, the same as the Dict project's (BL-1472), escapes CR, LF and control characters so selectors, replies and transcripts stay on one line, and formats a `TransferResult`.
- Tests that ignored the transfer result or the thrown exception now capture it to print it; no URL, scripted read, data row or assertion changed. Three tests that passed `new TranscriptTransferEvents()` inline now hold it in a local so its transcript can be printed.
- Counts, excluding `obj` (before -> after): `Assert.` 99 -> 99, `[TestMethod` 74 -> 74, `[DataRow(` 22 -> 22. Run total 87 -> 87: 82 run on Windows and the 5 off-Windows-only tests are skipped there. The run printed 82 `END` lines, one per test run, and none has a zero arrange, act or assert count.
- SLOW: none. The slowest test took 34 ms.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Curl.Protocol.Gopher.UnitTests test writes ARRANGE, ACT and ASSERT/DIFF diagnostics; counts unchanged, tests green.
