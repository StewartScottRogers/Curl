---
id: BL-1479
title: Make every test in Curl.Protocol.Mqtt.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1479 — Make every test in Curl.Protocol.Mqtt.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Protocol.Mqtt.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Protocol.Mqtt.UnitTests`, which tests `Curl.Protocol.Mqtt.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 123 test methods in 6 files, with 11 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the URL and options, each MQTT packet sent and received (through `Fakes/FakeConnector.cs` and `GatedConnection.cs`) as `BYTES` with its decoded type, `ManualTimeProvider` advances as `ARRANGE` lines, the payload written and the `CurlExitCode` with its error text.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Protocol.Mqtt.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Mqtt.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Mqtt.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Mqtt.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Sized for one run: 123 test methods in 6 files.
- 2026-10-07 (lane 2): a new `MqttDiagnostics.cs` in the project writes the shared lines: the URL
  and options (post data as `BYTES`, credentials as lengths only, max file size, proxy), each
  scripted read as `BYTES` labelled with its decoded MQTT packets (`CONNACK(2) SUBACK(3)`, with
  "cut short" for a partial packet), `ManualTimeProvider` advances, the result (exit code by
  name and number, bytes, error text), the bytes sent with their packets, the output, the `-v`
  transcript's last 8 lines escaped, and the diagnostic log's lines. Each class's own run
  helper calls it, so every test gets its ARRANGE and ACT lines; each test adds ASSERT or DIFF
  lines beside its unchanged assertions. To support this `ScriptedConnection` now exposes its
  scripted `Reads` and `FakeConnector` its `Connection`; both are read-only additions.
- The 5 skipped tests are the OS-conditioned socket-error tests excluded on this platform; they
  never start, so they write no `END`. The run counts 131 tests: 126 passed, each with one
  `END` line (126), none with a zero count, and 5 skipped. Before the task the total was also
  131 (no test attribute changed).
- Counts (`Select-String -AllMatches`, excluding `obj`/`bin`): before `Assert.` 216,
  `[TestMethod` 123, `[DataRow(` 11; after 216, 123, 11. The new lines go through
  `Diagnostics.*`, which does not match `Assert.`, so the counts are exactly the test's own.
- SLOW: none. The slowest test took 84 ms. The keep-alive tests write a `PHASE subscribe until
  idle` line for their real-clock wait.

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Curl.Protocol.Mqtt.UnitTests test writes ARRANGE, ACT and ASSERT/DIFF diagnostics with decoded MQTT packets
