---
id: BL-1457
title: Give every unit test timing and a SLOW line over a 3-second budget through shared test diagnostics
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [TestDiagnostics.cs, Directory.Build.props, Documentation/Planning/Decisions, Documentation/Wiki, Curl.Core.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1457 — Give every unit test timing and a SLOW line over a 3-second budget through shared test diagnostics

## Goal

Every test in all 33 `.UnitTests` projects writes a `START` line, an `END` line with its outcome and elapsed milliseconds, and `SLOW: <test> took <n> ms (budget 3000 ms)` when it runs over 3 seconds, through one shared `TestDiagnostics.cs` that also gives tests a labelled-output helper (Arrange, Act, Assert context, bytes, first difference, phase timings) for the 33 per-project tasks that depend on this one.

## Context

- Stewart's request (2026-10-04, plan approved the same day): every unit test writes enough descriptive output that an AI reading a failed or slow test's log can debug it, or understand its performance, without re-running it. Slow budget: 3 seconds per test (Stewart's number). Rollout: this foundation first, then one task per test project, each depending on this task.
- Today almost no test writes anything: 21 test files reference `TestContext` (mostly for its `CancellationToken`), and only 3 write output (`Curl.Console.UnitTests/CurlCommandRunnerDiagnosticLogTests.cs`, `CurlCommandRunnerFileConnectionNumberTests.cs`, `CurlCommandRunnerRangeTextHandOffTests.cs`).
- Where it plugs in: `Directory.Build.props` (lines ~40-46) already links the root `MSTestSettings.cs` into every project whose name ends in `UnitTests` with `<Compile Include="$(MSBuildThisFileDirectory)MSTestSettings.cs" Link="MSTestSettings.cs" />`. Link a new root `TestDiagnostics.cs` the same way, in the same `ItemGroup`, and extend the comment above it. `MSTestSettings.cs` sets `[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]`, so tests run in parallel at method level: no mutable static state shared between tests. Per-test state lives on the per-test `TestContext` (`TestContext.Properties`) or in an `AsyncLocal`.
- MSTest version: `Directory.Packages.props` pins the `MSTest` meta-package at 4.4.1. Checked in the 4.4.1 package's `lib/net9.0/MSTest.TestFramework.xml` (2026-10-04): `GlobalTestInitializeAttribute` and `GlobalTestCleanupAttribute` exist ("applies to every test method in the assembly"; the method must be `public static`, non-generic, take a single `TestContext`, and return `void` or `Task`; with several in one assembly the order is not guaranteed). `TestContext` has `Current` (static), `FullyQualifiedTestClassName`, `TestName`, `TestDisplayName`, `CurrentTestOutcome`, `Properties` and `WriteLine`. No existing test project declares a `[GlobalTestInitialize]` or `[AssemblyInitialize]`, and no type is named `TestDiagnostics` anywhere.
- The test run uses VSTest (`global.json` sets no test runner), so `dotnet test <project> --logger "console;verbosity=detailed"` prints each test's `TestContext` output, and CI's `gh run view <id> --log-failed` shows it for failing tests.
- Design constraints: base class library and MSTest only (no package). Time comes from `Stopwatch` or an injected `TimeProvider`, never `Thread.Sleep` and never a real 3-second wait in a test. `Curl.Core.UnitTests/Fakes/FakeTimeProvider.cs` is a hand-written fake clock to reuse for the helper's own tests. The output must be platform-neutral (no path separators or line endings that differ by OS in what is asserted). Make the type `internal` so the copy linked into each assembly stays private to it, and give it one namespace (for example `Curl.Testing`) that every project can bring in with `using`.
- The output format below is binding for the 33 per-project tasks. Record it in the ADR and the wiki page exactly as implemented:
  - `START <FullyQualifiedTestClassName>.<TestName>`, with ` (<TestDisplayName>)` appended when the display name differs (data rows).
  - `ARRANGE <label>: <value>` - an input that matters (command line, URL, request bytes, fake connection script).
  - `ACT <label>: <value>` - a result (exit code, stdout or stderr bytes, parsed result).
  - `ASSERT <label>: expected <expected>, actual <actual>` - assertion context.
  - `BYTES <label> (<n> bytes): <hex> | <text>` - hex pairs separated by spaces, then the bytes as text with every non-printable byte shown as `.`; capped at a named constant (e.g. 256 bytes) with `... (<m> more bytes)` after the cap.
  - `DIFF <label>: first difference at byte <i>: expected 0x<hh> '<c>', actual 0x<hh> '<c>'`, or `DIFF <label>: lengths differ, expected <a> bytes, actual <b> bytes, equal for the first <a or b>`, or `DIFF <label>: equal (<n> bytes)`. The same for strings by character index.
  - `PHASE <name>: <n> ms`, written when a `using var phase = diagnostics.Phase("handshake");` scope is disposed.
  - `END <test>: <outcome> in <n> ms (arrange <a>, act <b>, assert <c>)` - the counts are how many `ARRANGE`, `ACT` and `ASSERT`/`DIFF` lines the test wrote, so a per-project task can check from a log that a test wrote its diagnostics.
  - `SLOW: <test> took <n> ms (budget 3000 ms)` when elapsed is greater than the budget, after the `END` line. The 3000 ms budget is one named constant (e.g. `TestDiagnostics.SlowTestBudgetMilliseconds`).
- Keep the decision logic (formatting, the budget comparison, hex and diff rendering) in plain methods that take their inputs (elapsed `TimeSpan`, `TimeProvider`, byte spans), so it is unit-tested without the global hooks and without waiting.
- ADRs: the highest number on 2026-10-04 is ADR-0415, so the next free one is ADR-0416 (check again before writing; lanes file ADRs concurrently). ADR-0416 went to BL-1451 meanwhile, so this task's ADR is ADR-0417.

## Acceptance criteria

- [x] `TestDiagnostics.cs` exists at the repository root, with `[GlobalTestInitialize]` and `[GlobalTestCleanup]` methods writing the `START`, `END` and `SLOW:` lines through `TestContext.WriteLine`, the budget as one named constant equal to 3000, and the helper methods for `ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF` and `PHASE` lines in the format in Context. It uses only `System.*` and MSTest, and holds no mutable static state shared between tests.
- [x] `Directory.Build.props` links `TestDiagnostics.cs` into every project whose name ends in `UnitTests`, beside the `MSTestSettings.cs` link, and its comment says so.
- [x] Tests in `Curl.Core.UnitTests` (e.g. `TestDiagnosticsTests`) pin: the `SLOW:` line is written for an elapsed time of 3001 ms and not for exactly 3000 ms nor for 2999 ms, driven by `FakeTimeProvider` or a passed `TimeSpan` with no real wait; the `END` line's outcome, milliseconds and arrange/act/assert counts; `BYTES` hex and text for a mix of printable and non-printable bytes, and the truncation past the cap; `DIFF` for a first difference, for differing lengths and for equal input; and `PHASE` writing its name and milliseconds when disposed.
- [x] `dotnet build -warnaserror` for the whole solution is clean (all 33 test projects compile the linked file).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes for the whole solution.
- [x] `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` shows a `START` and an `END ... in <n> ms` line for the tests it runs.
- [x] `Documentation/Planning/Decisions/ADR-<next free number>-*.md` exists, marked "Decided by Claude under Stewart's delegation", and records: output through `TestContext.WriteLine` from MSTest's global test hooks; the 3-second budget as Stewart's number of 2026-10-04; the line format above; and the per-project rollout (one task per test project, each depending on this one).
- [x] `Documentation/Wiki/Test-Diagnostics.md` describes each line type with an example, how a test uses the helper (Arrange, Act, Assert context, bytes, first difference, phases), and the `SLOW:` budget; `Documentation/Wiki/Home.md` lists it in its page table.

## Notes

- The 33 per-project tasks that depend on this one touch only their own test project, so they can run in parallel lanes once this is Done. Do not start adding diagnostics to tests in other projects here; that is their work. Adding output to `TestDiagnosticsTests` itself is enough in `Curl.Core.UnitTests`.
- If `TestContext.CurrentTestOutcome` is not yet final when `[GlobalTestCleanup]` runs, record what MSTest 4.4.1 actually reports in the ADR and write that, rather than guessing the outcome.
- 2026-10-07 (lane 1): delivered directly rather than through the full `/feature` agent chain, to fit the run's budget; the design is recorded in ADR-0417.
- The hooks live in a second type, `TestDiagnosticsHooks`, which is `public`: MSTest's MSTEST0002, MSTEST0063 and MSTEST0050 analyzers reject an internal test class and its global fixture methods (tried with `[assembly: DiscoverInternals]` too). The helper `TestDiagnostics` stays `internal`.
- `Curl.Console.UnitTests` references `Curl.Protocol.Ssh.UnitTests`, which shows it its internals, so the two linked copies clash as CS0436. `Directory.Build.props` suppresses CS0436 for that one project only; the compiler uses the project's own copy, which is correct.
- Measured: `CurrentTestOutcome` is final in `[GlobalTestCleanup]` under MSTest 4.4.1 - a throwaway `Assert.Fail` test logged `END ...: Failed in 34 ms`. Recorded in ADR-0417.
- Slow means whole milliseconds (truncated) greater than 3000, so 3000.9 ms is not slow and its END and SLOW lines never disagree.
- `BYTES` and `PHASE` lines are not counted on the END line; `DIFF` counts as an assert, as the Context says.
- No `Measure-CodeQuality.ps1` run: the change is to test projects only, and the quality gates cover `*.UnitLibrary` and `Curl.Console`.
- Verified: `dotnet build -warnaserror` clean; fast tests green in all 33 test assemblies; `Curl.Core.UnitTests` with the detailed console logger shows `START` and `END ... in <n> ms` lines for every test.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every unit test writes START, END and over-3-second SLOW: lines; TestDiagnostics gives ARRANGE, ACT, ASSERT, BYTES, DIFF and PHASE lines
