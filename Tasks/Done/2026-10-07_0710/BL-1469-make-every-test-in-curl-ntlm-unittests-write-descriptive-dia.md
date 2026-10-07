---
id: BL-1469
title: Make every test in Curl.Ntlm.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Ntlm.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1469 — Make every test in Curl.Ntlm.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Ntlm.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Ntlm.UnitTests`, which tests `Curl.Ntlm.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 59 test methods in 10 files, with 9 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the type 1, 2 and 3 messages as `BYTES` with their decoded flags, the user, domain and workstation (test credentials are fake, so print them), and the challenge, timestamp, client nonce and responses as hex, with a `DIFF` against the expected message.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Ntlm.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Ntlm.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Ntlm.UnitTests -warnaserror` is clean and `dotnet test Curl.Ntlm.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Ntlm.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Sized for one run: 59 test methods in 10 files.
- Every test class gained a `TestContext` property and calls `TestDiagnostics.For(TestContext)`. Per-class helpers write the shared lines: `ArrangeMessage`/`ActEncoding` (type 3 flags as hex and names, domain, user, workstation, LM/NT responses and the encoded message as `BYTES`), `ArrangeChallenge`/`ActMessage` (challenge flags, server and client challenge, target information, the fake user name and password, the fixed time), `ActDecoding`/`Decoded`/`AssertFails` (the type 2 message as `BYTES` with its decoded flags, challenge, target name, target information and version), `ArrangeCommonValues`/`ActResponses` (MS-NLMP 4.2.1's values and every response and key as hex). Expected against actual hashes, keys and messages go through `DIFF`.
- Assertions check what they checked before. Where a test asserted on an inline call (`Assert.IsTrue(message.TryEncode(out ...))`, `Assert.AreEqual(hex, Convert.ToHexString(Compute...(...)))`), the result is now held in a local so it can be printed first and then asserted as before; nullable flow then needed `encoded!` on four lines. `Assert.ThrowsExactly` results are kept so the parameter name is printed.
- Counts (Select-String -AllMatches, excluding obj), before -> after: `Assert.` 119 -> 119, `[TestMethod` 59 -> 59, `[DataRow(` 9 -> 9. Total test count 66 -> 66.
- Detailed run: 66 `END` lines for 66 tests, none with `arrange 0`, `act 0` or `assert 0`; slowest test 18 ms.
- SLOW: none. No test printed a `SLOW:` line, so no follow-up task was filed.
- Only `Curl.Ntlm.UnitTests` changed, no library, so `Measure-CodeQuality.ps1` was not run (test projects are outside the coverage gate).

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Curl.Ntlm.UnitTests test writes ARRANGE, ACT and ASSERT/DIFF lines with NTLM messages, flags, credentials and responses
