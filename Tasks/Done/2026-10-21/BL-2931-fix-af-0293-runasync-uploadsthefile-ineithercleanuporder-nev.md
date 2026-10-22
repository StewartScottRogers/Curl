---
id: BL-2931
title: Fix AF-0293: RunAsync_UploadsTheFile_InEitherCleanupOrder never checks that the file was uploaded
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.LibTest.UnitTests]
requirement: none
created: 2026-10-21
completed: 2026-10-21
---
# BL-2931 — Fix AF-0293: RunAsync_UploadsTheFile_InEitherCleanupOrder never checks that the file was uploaded

## Goal

The defect the audit office reported as AF-0293 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0293 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0293-runasync-uploadsthefile-ineithercleanuporder-never.md`.

Location: `Curl.Conformance.LibTest.UnitTests/Lib525To533Tests.cs:12`

Location: `Curl.Conformance.LibTest.UnitTests/Lib525To533Tests.cs:12`

Step 1 sample. The test writes "Moooooooooooo\n" to a temporary file, runs Lib525 for 525 and 529, and asserts only result.ExitCode == 0 and ProgramName == "lib525". The FakeCurlEngine has no Performing hook, so nothing reads the upload callback or checks the bytes, the upload flag or the file size. A port that never set up the upload would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.LibTest.UnitTests/Lib525To533Tests.cs -Pattern 'RunAsync_UploadsTheFile_InEitherCleanupOrder' -Context 0,12
```

- Expected: The body asserts the uploaded bytes (or the upload options) the name promises.
- Actual: Only Assert.AreEqual(0, result.ExitCode) and Assert.AreEqual("lib525", ...ProgramName).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now hooks the fake multi handle's perform (`engine.Multi.Performing`), reads the added transfer's options while Lib525 still holds the file open, and asserts the URL, the upload flag, `InFileSize` 14, FTPPORT `-` (`Protocols.Ftp.ActiveAddress`), and that the read callback yields exactly `Moooooooooooo\n` (14 bytes), for 525 and 529. A port that never set up the upload now fails. The new assertions sit just past the reproduction's 12-line context; `-Context 0,26` shows them all.
- Test-only change, so no Measure-CodeQuality run. `dotnet build` clean; fast tests: 43 projects passed, 0 failed.

## Log

- 2026-10-21: Created.
- 2026-10-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-21: Doing -> Done. Lib525's upload test now asserts the uploaded bytes, upload flag, file size and FTPPORT for 525 and 529
