---
id: BL-1485
title: Make every test in Curl.Protocol.Telnet.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1485 — Make every test in Curl.Protocol.Telnet.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Protocol.Telnet.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Protocol.Telnet.UnitTests`, which tests `Curl.Protocol.Telnet.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 127 test methods in 9 files, with 124 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the URL and `-t` options, the negotiation bytes sent and received as `BYTES` with the decoded IAC commands and options, the data written out, and the `CurlExitCode` with its error text.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Protocol.Telnet.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Telnet.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Telnet.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Sized for one run: 127 test methods in 9 files.
- Done 2026-10-07. A new `TelnetDiagnostics.cs` (extension methods on `TestDiagnostics`) writes the URL and `-t` options, each scripted read as `BYTES` with its IAC commands decoded by name (`IAC WILL ECHO`, `IAC SB TTYPE [1 bytes] IAC SE`, `data(n)`), the bytes sent likewise, the output, transcripts and diagnostic-log lines, and the result as `SendError (55)` with its error text. Each class's `RunAsync` helper became an instance method so it writes the ARRANGE and ACT lines (and a `PHASE session` line in the two exchange-based classes); every test then writes ASSERT or DIFF lines mirroring its own assertions, and the tests that build their own context write their ARRANGE and ACT lines inline.
- Counts (`Select-String -AllMatches`, excluding `obj`), before -> after: `Assert.` 260 -> 260, `[TestMethod` 127 -> 127, `[DataRow(` 124 -> 124. In `TelnetProtocolHandlerTransferEventsTests`, six expected transcripts moved from inline arguments into a local `expected` passed to both the ASSERT line and the unchanged `CollectionAssert.AreEqual`; nothing they check changed.
- Run: 229 tests, 226 passed and 3 skipped (the OS-conditioned send-failure tests for the other platform, which MSTest never starts, so they write no START or END); 226 `END` lines, and none with `arrange 0`, `act 0` or `assert 0`.
- No test printed a `SLOW:` line; no follow-up task needed.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Telnet unit test writes ARRANGE, ACT and ASSERT/DIFF diagnostics; build clean, tests green
