---
id: BL-1459
title: Make every test in Curl.Cli.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-04
completed:
---
# BL-1459 — Make every test in Curl.Cli.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Cli.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Cli.UnitTests`, which tests `Curl.Cli.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 1536 test methods in 110 files, with 2782 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [ ] `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [ ] The run's total test count is unchanged, and in `Curl.Cli.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [ ] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [ ] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Large: 1536 test methods in 110 files is probably more than one `/task-run` can finish. Stewart asked for one task per project, so it is filed whole. If the runner judges it too big, it splits it before changing any test: it files tasks that each cover a range of the project's files by name (each `-Pipeline direct -DependsOn BL-1457 -Touches Curl.Cli.UnitTests`, with these criteria limited to its files' classes through `--filter "FullyQualifiedName~<class>"`), adds them to this task's `depends-on`, and moves this task back to `Backlog`; this task then only runs the whole-project checks above.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
