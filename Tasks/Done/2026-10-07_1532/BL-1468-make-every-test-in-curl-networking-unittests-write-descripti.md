---
id: BL-1468
title: Make every test in Curl.Networking.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1552, BL-1553, BL-1554, BL-1555, BL-1556, BL-1557, BL-1541, BL-1542, BL-1543, BL-1544, BL-1545, BL-1546, BL-1547, BL-1548, BL-1549, BL-1550, BL-1551]
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1468 — Make every test in Curl.Networking.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Networking.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Networking.UnitTests`, which tests `Curl.Networking.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 1887 test methods in 201 files, with 1470 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: endpoints, DNS replies (`Fakes/DnsTestReplies.cs`), proxy and TLS settings, the bytes passed through the fake transports, certificates by subject and thumbprint, and `PHASE` lines for resolve, connect, proxy handshake and TLS handshake.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Networking.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Networking.UnitTests -warnaserror` is clean and `dotnet test Curl.Networking.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Networking.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Large: 1887 test methods in 201 files is probably more than one `/task-run` can finish. Stewart asked for one task per project, so it is filed whole. If the runner judges it too big, it splits it before changing any test: it files tasks that each cover a range of the project's files by name (each `-Pipeline direct -DependsOn BL-1457 -Touches Curl.Networking.UnitTests`, with these criteria limited to its files' classes through `--filter "FullyQualifiedName~<class>"`), adds them to this task's `depends-on`, and moves this task back to `Backlog`; this task then only runs the whole-project checks above.
- 2026-10-07 (lane 6): split as above. 1892 test methods in 201 files (counted 2026-10-07 by the same patterns) is far more than one run's time and token budget. All test files are flat in the project root and every class is in namespace `Curl.Networking`, so the 17 tasks cover contiguous name ranges, 42 to 138 tests each: BL-1552 (A to Di), BL-1553 (Dns), BL-1554 (Doh, E, F), BL-1555 (HandBuiltTlsProviderTests), BL-1556 (other Ha and Http), BL-1557 (K to O), BL-1541 (P), BL-1542 (Q to Sy, outside SslStreamTlsProviderTests), BL-1543 and BL-1544 (SslStreamTlsProviderTests' 19 partial files, 1544 depending on 1543), BL-1545 (TcpConnectionListener, TcpConnectorQuicTests), BL-1546 to BL-1549 (TcpConnectorTests' 47 partial files in four chained ranges), BL-1550 (TcpDialer to Tls), BL-1551 (U). The two big partial classes are split by file, and the last task of each chain checks the whole class, because a test filter can select a class but not a file. Once they are all Done, this task runs only the whole-project checks and records the before and after counts.
- 2026-10-07 (lane 1): whole-project checks, all 17 range tasks Done, at `6314dd472`.
  - `dotnet build Curl.Networking.UnitTests -warnaserror`: 0 warnings, 0 errors.
  - Detailed fast run: 3063 tests, 3035 passed, 28 skipped, 0 failed. 3041 `END` lines: one per passed test (3035) plus 6 for tests that ran and called `Assert.Inconclusive` (the console logger counts those as skipped). The other 22 skipped tests are excluded by `[OSCondition]` before they start, so no code of theirs runs and no `END` line is possible; every test that executed wrote one. The `arrange 0` / `act 0` / `assert 0` pattern matched nothing.
  - Counts, "before" = `7794e40e0` (the parent of the first range task's commit, `10fde459d`, BL-1546), "after" = `HEAD`: `Assert.` 3866 -> 3848, `[TestMethod` 1892 -> 1883, `[DataRow(` 1470 -> 1468. The drops are not this task's work: Stewart's `6ecb2781f` (2026-10-07, ADR-0421) moved 11 test methods (36 `Assert.`, 2 `[DataRow(`) into `Curl.Networking.IntegrationTests`, and BL-1608's `dc4a4daaf` added 2 test methods (2 `Assert.`). Adjusted for those two commits the before counts are 3832, 1883 and 1468, so the diagnostics commits left `[TestMethod` and `[DataRow(` exactly unchanged and added 16 `Assert.` matches. The run's total test count likewise changed only by those moves.
  - `SLOW:` lines: none. The whole run took 8.9 s for 3063 tests.
  - This run changed only this task file.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Split into BL-1552 to BL-1551 (one per range of test files); this task runs the whole-project checks once they are Done
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every executed Curl.Networking.UnitTests test writes ARRANGE, ACT and ASSERT diagnostics with an END line; counts reconciled, no SLOW tests
