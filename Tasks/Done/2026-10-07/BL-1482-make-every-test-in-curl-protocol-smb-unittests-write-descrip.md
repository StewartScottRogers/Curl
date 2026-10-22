---
id: BL-1482
title: Make every test in Curl.Protocol.Smb.UnitTests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Smb.UnitTests]
requirement: none
created: 2026-10-04
completed: 2026-10-07
---
# BL-1482 — Make every test in Curl.Protocol.Smb.UnitTests write descriptive diagnostic output

## Goal

Every test in `Curl.Protocol.Smb.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases, so an AI reading a failed or slow test's log can debug it or understand its time without re-running it; no test's logic or assertions change.

## Context

- Stewart's request, plan approved 2026-10-04: every unit test writes enough descriptive console output that an AI reading a failed or slow test's log can debug it without re-running it; slow budget 3 seconds per test. One task per test project; this one is `Curl.Protocol.Smb.UnitTests`, which tests `Curl.Protocol.Smb.UnitLibrary`.
- BL-1457 links the root `TestDiagnostics.cs` into every test project and writes each test's `START`, `END ... (arrange <a>, act <b>, assert <c>)` and `SLOW:` lines itself. The line format (`ARRANGE`, `ACT`, `ASSERT`, `BYTES`, `DIFF`, `PHASE`) and how a test calls the helper are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; follow them and add no prefix of your own.
- Size, counted 2026-10-04 by matching `[TestMethod]`, `[TestMethod(` and `[DataTestMethod]` in the project's `.cs` files: 115 test methods in 17 files, with 51 `[DataRow(` lines. Solution-wide, 21 test files reference `TestContext` (mostly for its `CancellationToken`) and only 3, all in `Curl.Console.UnitTests`, write any output today.
- What matters here: the URL and share path, each SMB message sent and received (`SmbRecordedExchange.cs` holds them) as `BYTES` with its decoded command and status, the file bytes transferred, and the `CurlExitCode` with its error text; `PHASE` lines for negotiate, session setup, tree connect and transfer.
- Output only. No test method, data row, assertion or arrange step is removed, weakened or changed in what it tests. A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them.
- Keep the log readable: large payloads go through `BYTES` (which caps itself), never a loop printing thousands of lines. Nothing printed may make a test depend on the operating system.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Smb.UnitTests --filter "TestCategory!=Integration" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs (as many `END` lines as the run's total test count), and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing: every test wrote at least one `ARRANGE`, one `ACT` and one `ASSERT` or `DIFF` line.
- [x] The run's total test count is unchanged, and in `Curl.Protocol.Smb.UnitTests` (excluding `obj`) the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (each counted with `Select-String -AllMatches`) are no lower than before the task; the before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Smb.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Smb.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Smb.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; for each that is a real performance problem a follow-up task is filed and its ID is in Notes.

## Notes

- Sized for one run: 115 test methods in 17 files.
- 2026-10-07 (lane 2): a new `SmbDiagnostics.cs` in the project writes the shared lines: the URL
  and options (credentials as user name and password length only, upload size, no body, max
  file size, proxy), each scripted reply as `BYTES` labelled with its decoded SMB command and NT
  status (`NT_CREATE_ANDX 0xC0000034`, with "cut short" for a partial frame), any scripted write
  or read failure, the result (exit code by name and number, bytes, error text, report, remote
  time), the bytes sent with their messages decoded, the output, the `-v` transcript and the
  diagnostic log. Each class's run helper (`RunAsync` / `Transfer` / `Download` / `Upload` /
  `Reader` / `Establisher`) calls it and times the call as `PHASE execute` or `PHASE transfer`;
  each test adds ASSERT or DIFF lines beside its unchanged assertions. `ScriptedConnection` now
  exposes its scripted `Reads` (read-only). Per-step PHASE lines (negotiate, session setup, tree
  connect) were not added: the handler runs all steps in one call, so a test can time only the
  whole; the decoded reply and sent lists show which step a failure stopped at.
- The 10 skipped tests are the OS-conditioned socket-error tests excluded on Windows; they never
  start, so they write no `END`. The run counts 150 tests: 140 passed, each with one `END` line
  (140), none with a zero count, and 10 skipped. No test attribute changed, so the total before
  the task was also 150.
- Counts (`Select-String -AllMatches`, excluding `obj`/`bin`): before `Assert.` 227,
  `[TestMethod` 115, `[DataRow(` 51; after 227, 115, 51. The new lines go through
  `Diagnostics.*`, which does not match `Assert.`.
- SLOW: none. The slowest test took 47 ms (`TransferAsync_UploadLargerThanOneWrite_WritesCurlsPiecesInOrder`).

## Log

- 2026-10-04: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every Curl.Protocol.Smb.UnitTests test writes ARRANGE, ACT and ASSERT/DIFF diagnostics with decoded SMB messages
