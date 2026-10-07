---
id: BL-1461
title: Make every test in Curl.Console.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1577, BL-1578, BL-1579, BL-1580, BL-1581, BL-1582, BL-1583, BL-1584, BL-1585, BL-1586, BL-1587, BL-1588, BL-1589, BL-1590, BL-1591, BL-1592, BL-1593, BL-1594, BL-1595, BL-1596]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-04
completed:
---
# BL-1461 — Make every test in Curl.Console.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Console.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Console.UnitTests`, which tests `Curl.Console`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 1927 test methods in 221 files, with 1068 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in this project (`CurlCommandRunnerDiagnosticLogTests.cs`, `CurlCommandRunnerFileConnectionNumberTests.cs`, `CurlCommandRunnerRangeTextHandOffTests.cs`), write any output today; move their output onto the helper's format.
- What matters here: the full command line, the script of `ScriptedConnector`, `EndPointScriptedConnector`, `ScriptedDatagramConnector` or `ScriptedMultiplexedConnection` (what the fake server sends and expects), the request bytes Curl sent, stdout and stderr as `BYTES` or quoted text, and the exit code; `PHASE` lines for connect, handshake and transfer where a test drives a whole transfer.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it (the scripted connectors are the obvious place), as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [ ] `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [ ] The run's total test count is unchanged, and in `Curl.Console.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [ ] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.
- [ ] The task's commits change only files under `Curl.Console.UnitTests/` and this task file.
- [ ] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Large: 1927 test methods in 221 files is probably more than one `/task-run` can finish. Stewart asked for one task per project, so it is filed whole. If the runner judges it too big, it splits it before changing any test: it files tasks that each cover a range of the project's files by name (each `-Pipeline direct -DependsOn BL-1457 -Touches Curl.Console.UnitTests`, with these criteria limited to its files' classes through `--filter "FullyQualifiedName~<class>"`), adds them to this task's `depends-on`, and moves this task back to `Backlog`; this task then only runs the whole-project checks above.
- 2026-10-07 (lane 1): split before any test changed. Recounted: 221 test files, 1936 `[TestMethod`/`[DataTestMethod` matches. Filed BL-1577 to BL-1596, twenty contiguous ranges of the files in name order, about 90 to 126 test methods each (BL-1542's size, which finished in one run; BL-1458 hit its cost cap whole), with a partial class's files kept in one task so its class filter covers them all. Each names its files and the three that already write output carry the note to move it onto the helper. This task now waits on all twenty and then only runs the whole-project checks.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Split into BL-1577 to BL-1596 (twenty file ranges of Curl.Console.UnitTests); waits on them, then runs the whole-project checks
