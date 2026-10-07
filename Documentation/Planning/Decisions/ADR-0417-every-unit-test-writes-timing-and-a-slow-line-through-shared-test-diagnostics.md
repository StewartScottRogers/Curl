# ADR-0417 — Every unit test writes timing and a SLOW line through shared test diagnostics

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1457
- Decided by Claude under Stewart's delegation.

## Context

Stewart asked (2026-10-04) that every unit test write enough output for an AI reading a
failed or slow test's log to debug it, or understand its performance, without re-running
it, and set the slow budget at 3 seconds per test. Almost no test wrote anything. The 33
`.UnitTests` projects already share one linked file, `MSTestSettings.cs`, through
`Directory.Build.props`, and run in parallel at method level, so no test may share
mutable state with another. MSTest 4.4.1 has `[GlobalTestInitialize]` and
`[GlobalTestCleanup]`, which run around every test method in the assembly.

## Decision

1. One root file, `TestDiagnostics.cs`, is linked into every project whose name ends in
   `UnitTests`, beside `MSTestSettings.cs`. Its namespace is `Curl.Testing`.
2. `TestDiagnosticsHooks` holds the global hooks. It is `public`, because MSTest's
   MSTEST0002 and MSTEST0050 analyzers reject an internal test class and its global
   fixture methods. `TestDiagnostics`, the helper, is `internal`, so each assembly's copy
   stays its own. `Curl.Console.UnitTests` references `Curl.Protocol.Ssh.UnitTests`, which
   shows it its internals, so it sees both copies; `Directory.Build.props` suppresses
   CS0436 for that one project, since the compiler picks the project's own copy.
3. Every line goes through `TestContext.WriteLine`. Per-test state - the start time and
   the line counts - is a `TestDiagnostics` stored in the per-test `TestContext.Properties`;
   `TestDiagnostics.For(TestContext)` returns it, so the hooks and the test share it with
   no static state. Time comes from `TimeProvider` (the system clock in the hooks, a fake
   clock in the helper's own tests).
4. The budget is `TestDiagnostics.SlowTestBudgetMilliseconds = 3000`, Stewart's number of
   2026-10-04. A test is slow when its elapsed time, truncated to whole milliseconds, is
   greater than 3000: 3001 ms is slow, 3000 ms is not.
5. The line format, binding for the per-project tasks:
   - `START <FullyQualifiedTestClassName>.<TestName>`, with ` (<TestDisplayName>)` appended
     when the display name differs (data rows).
   - `ARRANGE <label>: <value>` - an input that matters.
   - `ACT <label>: <value>` - a result.
   - `ASSERT <label>: expected <expected>, actual <actual>`.
   - `BYTES <label> (<n> bytes): <hex> | <text>` - lower-case hex pairs separated by
     spaces, then the bytes as text with every byte outside 0x20-0x7e shown as `.`; at
     most `TestDiagnostics.ByteDisplayCap` (256) bytes, then ` ... (<m> more bytes)`.
   - `DIFF <label>: first difference at byte <i>: expected 0x<hh> '<c>', actual 0x<hh> '<c>'`,
     `DIFF <label>: lengths differ, expected <a> bytes, actual <b> bytes, equal for the first <min>`,
     or `DIFF <label>: equal (<n> bytes)`; for strings the same with `character` and
     `characters`.
   - `PHASE <name>: <n> ms`, when the scope from `Phase(name)` is disposed.
   - `END <test>: <outcome> in <n> ms (arrange <a>, act <b>, assert <c>)`, where the counts
     are the ARRANGE, ACT and ASSERT-or-DIFF lines the test wrote; BYTES and PHASE lines
     are not counted.
   - `SLOW: <test> took <n> ms (budget 3000 ms)`, after the END line, only over budget.
6. The outcome is `TestContext.CurrentTestOutcome` as MSTest 4.4.1 reports it in
   `[GlobalTestCleanup]`: measured on 2026-10-07, it is already final there - `Passed`
   for a passing test and `Failed` for one that called `Assert.Fail`.
7. Rollout: this task lays the foundation; one task per test project, each depending on
   BL-1457 and touching only its own project, adds ARRANGE, ACT, ASSERT, BYTES, DIFF and
   PHASE lines to that project's tests, so the lanes can run them in parallel.

## Consequences

- Every test's log shows its timing and outcome with no change to any test; a test over
  3 seconds is found by searching the log for `SLOW:`.
- `dotnet test <project> --logger "console;verbosity=detailed"` and CI's
  `gh run view <id> --log-failed` show the lines.
- The decision logic is in plain static methods (`FormatStart`, `FormatEnd`, `FormatSlow`,
  `FormatBytes`, `FormatDiff`), pinned by `Curl.Core.UnitTests/TestDiagnosticsTests.cs`
  without waiting.
