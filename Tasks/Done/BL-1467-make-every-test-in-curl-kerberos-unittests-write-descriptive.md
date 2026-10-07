---
id: BL-1467
title: Make every test in Curl.Kerberos.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Kerberos.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1467 — Make every test in Curl.Kerberos.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Kerberos.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Kerberos.UnitTests`, which tests `Curl.Kerberos.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 378 test methods in 41 files, with 414 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: KDC requests and replies exchanged with `FakeKdc`, `FakeKcm` or `FakeGssAcceptor` as `BYTES` with their message type, the principal, realm, encryption type and key usage, and decrypted ticket or authenticator fields; `PHASE` lines for AS, TGS and AP exchanges where a test runs several.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Kerberos.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Kerberos.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Kerberos.UnitTests -warnaserror` is clean and `dotnet test Curl.Kerberos.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Kerberos.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Sized for one run: 378 test methods in 41 files.
- Approach (choice taken, 2026-10-07): no test needed editing. `DiagnosticAssertions.cs`
  declares `Assert`, `CollectionAssert` and `StringAssert` in the tests' own namespace
  `Curl.Kerberos`, so they take precedence over MSTest's imported ones. Each writes, through
  `TestDiagnostics.For(TestContext.Current)`, `ARRANGE expected`, `ACT actual` (with `BYTES`
  lines for byte arrays) and an `ASSERT` line, or a `DIFF` line for strings and byte arrays,
  then calls MSTest's assertion with the same arguments, so no assertion changes what it
  tests. The `Throws*` ones write the expected exception type and what was thrown. Why:
  975 assertions across 378 tests, the same expected-against-actual context for each, and
  zero risk of changing a test's logic by hand. `TestContext.Current` is MSTest 4's
  experimental ambient context (`MSTESTEXP`, suppressed with a comment at its two uses).
- `FakeKdc.Answer` writes each KDC request as `BYTES KDC request <AsRequest|TgsRequest>,
  client, server, realm, etypes, padata` and its reply as `BYTES KDC reply to ...`;
  `FakeGssAcceptor` writes the AP-REQ token (etype, key usage 11) and the AP-REP token (etype,
  key usage 12, client time); `FakeKcm` writes each queued reply. 231 such lines in a run.
  These ordered BYTES lines mark the AS, TGS and AP exchanges; no separate `PHASE` lines were
  added since no test is near the 3-second budget.
- Counts in `Curl.Kerberos.UnitTests` (excluding `obj`), before -> after: `Assert.` 975 -> 990
  (the new file's forwarding calls), `[TestMethod` 378 -> 378, `[DataRow(` 414 -> 414. Run
  total 707 tests before and after, 707 passed; 707 `END` lines, none with a zero count.
- SLOW: none. No test printed a `SLOW:` line, so no follow-up task.
- Build: only this leaf test project changed (nothing references it), so
  `dotnet build Curl.Kerberos.UnitTests -warnaserror` and its fast tests are the gate;
  `dotnet format --verify-no-changes` is clean.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Curl.Kerberos.UnitTests test writes ARRANGE/ACT/ASSERT-or-DIFF lines through a namespace-local Assert shim, and the fake KDC, GSS acceptor and KCM write their messages as BYTES
