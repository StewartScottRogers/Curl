---
id: BL-1766
title: Fix AF-0088: ADR-0410 decision 3 says file:// reads a source's time with stat off Windows; off Windows the code reads nothing
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-08
completed: 2026-10-08
---
# BL-1766 — Fix AF-0088: ADR-0410 decision 3 says file:// reads a source's time with stat off Windows; off Windows the code reads nothing

## Goal

The defect the audit office reported as AF-0088 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0088 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0088-adr-0410-decision-3-says-file-reads-a-source-s-tim.md`.

Location: `Documentation/Planning/Decisions/ADR-0410-remote-file-times-travel-as-unix-seconds-so-r-can-stamp-and-cap-times-past-year-9999.md:65`

Location: `Documentation/Planning/Decisions/ADR-0410-remote-file-times-travel-as-unix-seconds-so-r-can-stamp-and-cap-times-past-year-9999.md:65`

Decision 3 says: '`Curl.Protocol.File.UnitLibrary` reads a source file's time as Unix seconds where .NET's `DateTime`-based `FileSystemInfo.LastWriteTimeUtc` cannot hold it (the Win32 `FILETIME` on Windows, `stat` off Windows, through `LibraryImport` ...)'. Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs:368-369 chooses `OperatingSystem.IsWindows() ? new Win32SourceLastWriteReader() : new NoRawSourceLastWriteReader()`. SourceLastWriteReaders.cs:26-33 describes NoRawSourceLastWriteReader as 'The reader for a platform with no raw read of its own yet: it reads nothing' and returns null. No `stat` LibraryImport exists in the library. So on Linux and macOS a source time past 9999 is not read, contrary to the ADR. Decision 4 says `utimensat` off Windows, but NativeFileTimeSetter.cs:61 imports `utimes` (as curl does); that detail is wrong too.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Documentation/Planning/Decisions/ADR-0410-*.md -SimpleMatch 'stat` off Windows').LineNumber; Select-String -Path Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs -SimpleMatch 'new Win32SourceLastWriteReader() : new NoRawSourceLastWriteReader()'
```

- Expected: Either the ADR no longer claims a stat read off Windows, or the handler picks a stat-based reader off Windows (no match for the second command).
- Actual: 65, then FileProtocolHandler.cs:369: OperatingSystem.IsWindows() ? new Win32SourceLastWriteReader() : new NoRawSourceLastWriteReader();

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fixed the ADR, not the code: this is a docs task whose `touches` is Documentation only.
  ADR-0410 decision 3 now says Windows reads the raw `FILETIME` with `GetFileTime` and off
  Windows `NoRawSourceLastWriteReader` reads nothing yet; decision 4 now names `utimes`, the
  call `NativeFileTimeSetter` imports. The missing off-Windows `stat` reader is filed as BL-1790.
- Reproduction now: the first command prints nothing (the ADR no longer claims `stat` off
  Windows); the handler line still matches, which the expected result's "either" allows.
- Edited directly rather than through align-and-document: a two-sentence ADR correction.
- Build clean; fast tests green (0 failing assemblies).

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. ADR-0410 decisions 3 and 4 describe the code as it is: FILETIME read on Windows only, no stat read off Windows yet (BL-1790), utimes off Windows
