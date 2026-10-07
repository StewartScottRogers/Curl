---
id: BL-1463
title: Make every test in Curl.Core.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457, BL-1534, BL-1528, BL-1529, BL-1530, BL-1531, BL-1532, BL-1533]
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1463 — Make every test in Curl.Core.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Core.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Core.UnitTests`, which tests `Curl.Core.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own. BL-1457 also adds the helper's own tests to this project (e.g. `TestDiagnosticsTests`); they already write diagnostics.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 635 test methods in 51 files, with 944 `[DataRow(` lines (before BL-1457's tests were added). Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the inputs to the type under test (URLs, options, multipart parts, redirect chains), the events recorded by `Fakes/RecordingTransferEvents.cs` and lines recorded by `Fakes/RecordingDiagnosticLog.cs`, `FakeTimeProvider` or `HandFiredTimeProvider` advances as `ARRANGE` lines, and the result, with multipart bodies through `BYTES` and `DIFF`.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Core.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Core.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Large: 635 test methods in 51 files is probably more than one `/task-run` can finish. Stewart asked for one task per project, so it is filed whole. If the runner judges it too big, it splits it before changing any test: it files tasks that each cover a range of the project's files by name (each `-Pipeline direct -DependsOn BL-1457 -Touches Curl.Core.UnitTests`, with these criteria limited to its files' classes through `--filter "FullyQualifiedName~<class>"`), adds them to this task's `depends-on`, and moves this task back to `Backlog`; this task then only runs the whole-project checks above.
- 2026-10-07 (lane 3): split as above. 634 test methods in 52 files is far more than one run's time and token budget. Seven tasks cover every test file except `TestDiagnosticsTests.cs`, which BL-1457 added already writing diagnostics, in groups of 72 to 115 tests: BL-1534 (AltSvc, ByteRangeParser), BL-1528 (FileSystem, Globbing), BL-1529 (Hsts, IpfsGatewayRewriter, watchdogs), BL-1530 (Multipart), BL-1531 (proxy, dispatcher, rate limit, redirect side files), BL-1532 (RedirectFollowerTests, redirect and retry policies), BL-1533 (TransferRetrier, UrlSchemeGuesser). Their filters use the tests' namespaces, which are `Curl.Core.*`, not `Curl.Core.UnitTests.*`. Once they are Done, this task runs only the whole-project checks and records the before and after counts.
- 2026-10-07 (lane 8): whole-project checks after BL-1534 and BL-1528..BL-1533. The first run showed all 16 `TestDiagnosticsTests` methods (17 runs) still ending `(arrange 0, ...)` or `assert 0`: they printed captured lines as ACT only, contrary to the split note. This run added ARRANGE, ACT and ASSERT/DIFF lines to each, through a `Log` property; every assertion is kept, three tests now assert on locals they first log rather than on inline calls. It also fixed a pre-existing `dotnet format` import-order error in `RedirectFollowerHstsTests.cs`.
- Result: `Total tests: 1448` (1442 passed, 6 skipped by `OSCondition` off this OS), before and after. 1442 `END` lines: the 6 skipped tests never run, so MSTest's hooks write no `END` for them - every test that runs writes one. The `Select-String` check prints nothing.
- Counts (`Select-String -AllMatches`, `Curl.Core.UnitTests` without `obj`/`bin`): before (at `fd7a44f2e`, after BL-1457 and before the split tasks) `Assert.` 1224, `[TestMethod` 651, `[DataRow(` 946; after 1232, 652, 946. The extra test and asserts came in with the split tasks and `027938da9`.
- SLOW: `Curl.Core.Hsts.HstsCacheTests.ApplyHeader_PastMaxEntries_DropsTheFirst` took 3099 ms once, on a loaded machine. Phases added to it: `PHASE read file: 1616 ms`, `PHASE apply header: 0 ms` (quiet run, 1.6 s total, no SLOW line). Real problem: `HstsCache.ReadFile` scans every held entry for each line, quadratic in 10000 entries. Follow-up filed: BL-1610.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Split into BL-1534..BL-1533 (one per range of test files); waits on them, then runs only the whole-project checks
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Curl.Core.UnitTests test that runs writes ARRANGE, ACT and ASSERT or DIFF lines; slow HSTS read filed as BL-1610
