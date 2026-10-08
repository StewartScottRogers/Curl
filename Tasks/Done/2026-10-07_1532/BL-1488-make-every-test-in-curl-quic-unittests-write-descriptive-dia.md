---
id: BL-1488
title: Make every test in Curl.Quic.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Quic.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1488 — Make every test in Curl.Quic.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Quic.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Quic.UnitTests`, which tests `Curl.Quic.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 280 test methods in 24 files, with 109 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: packets and frames as `BYTES` with their decoded type, packet number and connection IDs, keys, IVs and header-protection masks as hex with a `DIFF` against RFC 9001 test vectors where a test uses them, and `PHASE` lines for the handshake and data transfer.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Quic.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Quic.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Quic.UnitTests -warnaserror` is clean and `dotnet test Curl.Quic.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Quic.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Sized for one run: 280 test methods in 24 files.
- 2026-10-07 (lane 2): resumed from the earlier run's stash (applied as a diff, `git diff <sha>^1 <sha> -- Curl.Quic.UnitTests | git apply --3way`). The 5 missing `Assert.` calls were the six `Decode_*` tests in `QuicPacketNumberTests`, folded into one helper assertion; the helper is now `DecodeWithDiagnostics`, which writes the lines and returns the decoded value, and each test asserts it itself again. Added an `ACT` line to `OnLossDetectionTimeout_FirstInitialLost_ResendsTheClientHelloInANewPacketAndCompletes` and `Encode_CurlClientDefaults_MatchesCurlsBuild`.
- Counts in `Curl.Quic.UnitTests` `.cs` files, before -> after: `Assert.` 901 -> 901, `[TestMethod` 280 -> 280, `[DataRow(` 109 -> 109. Test run: 411 before, 411 after, 411 `END` lines, none with a zero arrange, act or assert count.
- SLOW: no test printed a `SLOW:` line (whole project ran in about 1.1 s), so no follow-up task.
- Only `Curl.Quic.UnitTests` changed and nothing references it, so the fast tests run were that project's (411/411); `dotnet build` of the solution is clean.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Session budget ran out with work in the stash: all 24 files instrumented, build clean, 411/411 tests pass; left: Assert. count fell 901->896 (restore the 5 Assert calls removed, mostly QuicLossRecoveryTests OnLossDetectionTimeout and QuicPacketProtectionTests multi-throw tests), and 2 tests lack a line: QuicClientConnectionStateTests.OnLossDetectionTimeout_FirstInitialLost_* and QuicTransportParametersTests.Encode_CurlClientDefaults_MatchesCurlsBuild (act 0)
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Curl.Quic.UnitTests test writes ARRANGE, ACT and ASSERT/DIFF diagnostics; 411/411 pass, assert counts unchanged
