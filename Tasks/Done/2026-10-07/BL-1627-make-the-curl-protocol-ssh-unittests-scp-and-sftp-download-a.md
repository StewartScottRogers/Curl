---
id: BL-1627
title: Make the Curl.Protocol.Ssh.UnitTests SCP and SFTP download and listing tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Protocol.Ssh.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1627 — Make the Curl.Protocol.Ssh.UnitTests SCP and SFTP download and listing tests write descriptive diagnostic output

## Goal

Every test in these files of `Curl.Protocol.Ssh.UnitTests` writes, through BL-1457's shared `TestDiagnostics` helper, the Arrange inputs that matter, its Act result and its assertion context (expected against actual, first differing byte or character), plus `PHASE` timings where it has distinct phases; no test's logic or assertions change. Files: Scp/ plus Sftp/SftpDirectoryListingTests.cs and Sftp/SftpFileDownloadTests.cs with its partials (.MaxFileSize, .Ranges, .RangeText) (about 104 test methods, counted 2026-10-07).

## Context

- Split from BL-1484: one task for the whole project (811 test methods in 94 files) was too big for one `/task-run`. BL-1484's Context applies in full: the line format and helper usage in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR; what matters for SSH (packets as `BYTES` with their decoded message number, negotiated algorithms, keys as hex, authentication steps, SFTP and SCP requests and replies, `PHASE` lines); output only; `BYTES` for large payloads; nothing OS-dependent. BL-1481 (`Curl.Protocol.Rtsp.UnitTests/RtspDiagnostics.cs`) is a worked example.
- A shared helper in this project (for example `SshDiagnostics.cs`) may be added or extended by whichever split task gets there first; the others reuse it.
- Classes: ScpCommandTests, ScpFileDownloadTests, ScpFileUploadTests, ScpHeaderNumberTests, ScpRemotePathTests, SftpDirectoryListingTests, SftpFileDownloadTests.

## Acceptance criteria

- [x] `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration&(FullyQualifiedName~<class>|...)" --logger "console;verbosity=detailed"` over this task's classes prints an `END` line for every test it runs, and piping that output to `Select-String -Pattern 'END .*(\(arrange 0,|, act 0,|, assert 0\))'` prints nothing.
- [x] The filtered run's test count is unchanged, and in this task's files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches (`Select-String -AllMatches`) are no lower than before; before and after numbers are recorded in Notes.
- [x] `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ssh.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Protocol.Ssh.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- New shared helper `Curl.Protocol.Ssh.UnitTests/Sftp/SshFileTransferDiagnostics.cs`: the URL path and scripted server bytes (`ARRANGE` + `BYTES`), the transfer's result, output bytes and progress (`ACT`), the client's SSH messages and SFTP requests by decoded name with their bytes (first 12), and `AssertResult`, `AssertText` (with `DIFF`), `AssertProgress` and `DiffRequests` (request count, then a `DIFF` per expected request). Reuses BL-1622's `SshAuthenticationDiagnostics` (`ActFailure`, `AssertFailure`, message names) and BL-1625's `SshKeyDiagnostics` (`ActBytes`, `AssertBytes`, `ArrangeText`).
- Each class's shared download, upload and listing helpers became instance methods that write the Arrange and Act lines and time the transfer in a `PHASE download`/`upload`/`list` line; the request-checking helpers write their `DIFF` lines, so every test using them gets its assertion context. Tests calling the code under test directly write their own lines. No `Assert` call, test logic or data row changed.
- Filtered run over ScpCommandTests, ScpFileDownloadTests, ScpFileUploadTests, ScpHeaderNumberTests, ScpRemotePathTests, SftpDirectoryListingTests and SftpFileDownloadTests: 264 tests before and after, 264 `END` lines, and the `Select-String` for `arrange 0`, `act 0` or `assert 0` prints nothing.
- Counts in this task's files (`Select-String -AllMatches`), before -> after: `Assert.` 233 -> 233, `[TestMethod` 104 -> 104, `[DataRow(` 201 -> 201.
- `dotnet build Curl.Protocol.Ssh.UnitTests -warnaserror` clean, `dotnet format --verify-no-changes` clean, and the project's fast tests pass (1673).
- SLOW: no test printed a `SLOW:` line; the slowest, the 3 MB SCP and SFTP downloads, run in about half a second.

- Lane 7's work (branch factory/BL-1627-lane-7-20261007-111121) was cherry-picked onto lane 3; its integration had failed only on unrelated Curl.Http2 and Curl.Cookies tests. Since then BL-1628 added `SftpDiagnostics.ActFailure`, which made four `ActFailure` calls in `ScpFileDownloadTests` and `ScpFileUploadTests` ambiguous (both `Sftp` and `Authentication` namespaces imported); they now call `SshAuthenticationDiagnostics.ActFailure` explicitly, the helper lane 7 meant. Rerun on lane 3: 264 filtered tests, 264 `END` lines, no `arrange 0`/`act 0`/`assert 0`, no `SLOW:`; solution build clean and all fast tests green.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Backlog. Lane 7 could not integrate: fast tests failed twice (Curl.Http2.UnitTests failed; then Curl.Cookies.UnitTests: EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother) after rebasing onto the other lanes' work. The work is on branch factory/BL-1627-lane-7-20261007-111121; start with git cherry-pick --no-commit factory/BL-1627-lane-7-20261007-111121 and fix it.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every SCP and SFTP download, upload and listing test in Curl.Protocol.Ssh.UnitTests writes ARRANGE, ACT and ASSERT/DIFF lines with PHASE timings through TestDiagnostics
