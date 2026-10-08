---
id: BL-1628
title: Make the Curl.Protocol.Ssh.UnitTests SFTP upload, quote and session tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1628 — Make the Curl.Protocol.Ssh.UnitTests SFTP upload, quote and session tests write descriptive diagnostic output

## Goal

Every test in these files of `Curl.Protocol.Ssh.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases; no test's logic or assertions change. Files: Sftp/ except SftpDirectoryListingTests and SftpFileDownloadTests* (about 90 test methods, counted 2026-10-07).

## Context

- Split from BL-1484: one task for the whole project (811 test methods in 94 files) was too big for one `/task-run`. BL-1484's Context applies in full: the line format and helper usage in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; what matters for SSH (packets as `BYTES` with their decoded message number, negotiated algorithms, keys as hex, authentication steps, SFTP and SCP requests and replies, `PHASE` lines); output only; `BYTES` for large payloads; nothing OS-dependent. BL-1481 (`Curl.Protocol.Rtsp.UnitTests/RtspDiagnostics.cs`) is a worked example.
- A shared helper in this project (for example `SshDiagnostics.cs`) may be added or extended by whichever split task gets there first; the others reuse it.
- Classes: SftpFileUploadTests, SftpQuoteCommandsTests, SftpQuoteCommandTests, SftpRemotePathTests, SftpSessionTests, SftpStatusCodeTests, SftpTransferQuoteTests.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration&(FullyQualifiedName~<class>|...)" --logger "console;verbosity=detailed"` over this task's classes prints an `END` line for every test it runs, and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing.
- [x] The filtered run's test count is unchanged, and in this task's files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (`Select-String -AllMatches`) are no lower than before; before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Added `Curl.Protocol.Ssh.UnitTests/Sftp/SftpDiagnostics.cs` (scripted server bytes, SFTP requests with decoded packet type and request id, transfer results, `SshTransferException`s). The other Sftp split tasks can reuse it.
- Choice: where a class routes its tests through shared helpers (`UploadAsync`, `AssertRequests`, `AssertStartFailsAsync`, `RunAsync`, the `Outcome` record), the helpers write the ARRANGE/ACT/ASSERT lines, so each test still writes all three without the same lines copied into every method. The helpers became instance methods so they can reach `TestContext`. Tests that pass no assertion (session start accepted) write an `ASSERT session started` line and gain no new `Assert.` call, so their logic stays as it was.
- The upload helper wraps the transfer in `PHASE upload`.
- Filtered run: 219 tests before and after, every one prints `END` with arrange, act and assert above 0.
- `Assert.` / `[TestMethod` / `[DataRow(` counts, before = after: SftpFileUploadTests 50/27/24, SftpQuoteCommandsTests 21/32/34, SftpQuoteCommandTests 14/5/39, SftpRemotePathTests 9/6/32, SftpSessionTests 7/11/6, SftpStatusCodeTests 2/1/23, SftpTransferQuoteTests 21/8/0.
- No test printed `SLOW:`. The slowest was `UploadAsync_ThreeMegabytes_SendsEach64KiBBlockAsWritesOf30000BytesAsMeasured`, at 1889 ms, inside the 3000 ms budget.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. all 219 tests in the seven Sftp classes write ARRANGE, ACT and ASSERT lines; build and fast tests green
