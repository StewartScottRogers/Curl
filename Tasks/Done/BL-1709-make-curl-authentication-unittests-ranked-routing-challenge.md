---
id: BL-1709
title: Make Curl.Authentication.UnitTests ranked, routing, challenge-scheme and diagnostic-log tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1708]
touches: [Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1709 — Make Curl.Authentication.UnitTests ranked, routing, challenge-scheme and diagnostic-log tests write descriptive diagnostic output

## Goal

Every test in the files named below writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Split from BL-1458 (2026-10-07), which hit the dark factory's cost cap as one task: `Curl.Authentication.UnitTests` has 426 test methods in 33 files, so it is done in five tasks, BL-1706 to BL-1710, chained by `depends-on` because all five touch the one project and the shared fakes. Each covers the files listed here; BL-1710 also checks the whole project.
- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. The project tests `Curl.Authentication.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- This task's files under `Curl.Authentication.UnitTests/` (86 test methods counted 2026-10-07 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]`):
- RankedHttpAuthenticatorTests.cs (34)
- RoutingSecurityContextFactoryTests.cs (16)
- HttpChallengeSchemesTests.cs (5)
- AuthDiagnosticLogTests.cs (14)
- AuthDiagnosticLogNetrcSigV4NegotiateTests.cs (9)
- SaslDigestMd5Tests.cs (8)
- Helpers these tests use (edit if needed): `Fakes/RecordingDiagnosticLog.cs`, `ScriptedSecurityContextFactory.cs`.
- What matters here: the scheme and challenge header the authenticator receives, the credentials and options it is given (test credentials are fake, so print them), the `Authorization` or `Proxy-Authorization` value it produces, and in tests driven by `ScriptedSecurityContext.cs` each scripted token as `BYTES`.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Edit a shared fake only as far as this task's tests need; the chain keeps the tasks from running together.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Authentication.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test in this task's files, and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints no line for a test in this task's files: each wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in this task's files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Authentication.UnitTests -warnaserror` is clean and `dotnet test Curl.Authentication.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Authentication.UnitTests/` and this task file.
- [x] Notes list every test in this task's files that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Sized for one run: 86 test methods in the files above.
- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before and after, unchanged: RankedHttpAuthenticatorTests 61/34/37, RoutingSecurityContextFactoryTests 39/16/6, HttpChallengeSchemesTests 5/5/22, AuthDiagnosticLogTests 25/14/7, AuthDiagnosticLogNetrcSigV4NegotiateTests 16/9/6, SaslDigestMd5Tests 10/8/25. Project total 819 tests, all passing.
- Where a test asserted only through `Assert.Throws*`, its return value is now captured so the exception can be printed; the call and its check are the same. Calls whose result was discarded now keep it in a local to print it.
- The detailed run's `END` lines for this task's files show no `arrange 0`, `act 0` or `assert 0`.
- SLOW: none of this task's tests printed a `SLOW:` line.
- The shared fakes (`Fakes/RecordingDiagnosticLog.cs`, `ScriptedSecurityContextFactory.cs`) needed no change.

## Log

- 2026-10-07: Created, split from BL-1458.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. All 86 tests in the six files write ARRANGE, ACT and ASSERT or DIFF lines; counts unchanged; 819 tests green
