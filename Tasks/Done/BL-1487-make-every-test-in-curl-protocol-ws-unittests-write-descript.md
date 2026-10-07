---
id: BL-1487
title: Make every test in Curl.Protocol.Ws.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Ws.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1487 — Make every test in Curl.Protocol.Ws.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Protocol.Ws.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Protocol.Ws.UnitTests`, which tests `Curl.Protocol.Ws.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 222 test methods in 24 files, with 164 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the URL, the upgrade request sent and the scripted `101` response, each WebSocket frame sent and received as `BYTES` with its decoded opcode, FIN and mask flags and payload length, and the `CurlExitCode` with its error text; `PHASE` lines for upgrade and frame exchange.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ws.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Protocol.Ws.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ws.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ws.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ws.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Sized for one run: 222 test methods in 24 files.
- Counts in `Curl.Protocol.Ws.UnitTests` (excluding `obj`), before -> after: `Assert.` 438 -> 438, `[TestMethod` 222 -> 222, `[DataRow(` 164 -> 164. Run total 355 -> 355 (350 passed, 5 skipped).
- `END` lines: 350, one per test that ran. The 5 skipped tests are `[OSCondition]` off-Windows socket tests, which MSTest skips before the global hooks run, so they write no `START`/`END`; on Linux and macOS they run and write their lines. The arrange-0/act-0/assert-0 `Select-String` check prints nothing.
- `Bytes` lines are not counted on `END`, so every test that logs its scripted reply as `BYTES` also writes an `ARRANGE` line (the input that matters: URL, head line, upload, frame tracing).
- Handler tests wrap `ExecuteAsync` in `PHASE upgrade` or `PHASE frame exchange` where the test drives a whole exchange; pure parser tests have no phases.
- SLOW: none. No test printed a `SLOW:` line; the whole project runs in under a second, so no follow-up task.
- Done with four parallel Sonnet test-writer agents, one per group of files, then a pass by hand for 16 tests whose only arrange output was a `BYTES` line.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in Curl.Protocol.Ws.UnitTests writes ARRANGE, ACT and ASSERT/DIFF lines, with BYTES for replies and frames and PHASE around handler exchanges
