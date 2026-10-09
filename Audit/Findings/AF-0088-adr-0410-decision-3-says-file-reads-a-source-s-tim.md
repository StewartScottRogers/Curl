---
id: AF-0088
title: ADR-0410 decision 3 says file:// reads a source's time with stat off Windows; off Windows the code reads nothing
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-10-09_0225.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_2315.md, 2026-10-09_0225.md).
key: truthfulness:Documentation/Planning/Decisions/ADR-0410-remote-file-times-travel-as-unix-seconds-so-r-can-stamp-and-cap-times-past-year-9999.md:SourceLastWriteReader:stale-adr
reproduction: none
task: BL-1766
tasks: BL-1766
found: 2026-10-08
found-at: 043959c94f40aaf4a1c37e70d0c6d957c5f1e564
scorecard: 2026-10-08_0748.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-08_2315.md, 2026-10-09_0225.md
---
# AF-0088 - ADR-0410 decision 3 says file:// reads a source's time with stat off Windows; off Windows the code reads nothing

## Summary

Medium finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-0410-remote-file-times-travel-as-unix-seconds-so-r-can-stamp-and-cap-times-past-year-9999.md:65`: ADR-0410 decision 3 says file:// reads a source's time with stat off Windows; off Windows the code reads nothing. Reported by an auditor flagged unreliable in 2026-10-08_0748.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-0410-remote-file-times-travel-as-unix-seconds-so-r-can-stamp-and-cap-times-past-year-9999.md:65`

Decision 3 says: '`Curl.Protocol.File.UnitLibrary` reads a source file's time as Unix seconds where .NET's `DateTime`-based `FileSystemInfo.LastWriteTimeUtc` cannot hold it (the Win32 `FILETIME` on Windows, `stat` off Windows, through `LibraryImport` ...)'. Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs:368-369 chooses `OperatingSystem.IsWindows() ? new Win32SourceLastWriteReader() : new NoRawSourceLastWriteReader()`. SourceLastWriteReaders.cs:26-33 describes NoRawSourceLastWriteReader as 'The reader for a platform with no raw read of its own yet: it reads nothing' and returns null. No `stat` LibraryImport exists in the library. So on Linux and macOS a source time past 9999 is not read, contrary to the ADR. Decision 4 says `utimensat` off Windows, but NativeFileTimeSetter.cs:61 imports `utimes` (as curl does); that detail is wrong too.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path Documentation/Planning/Decisions/ADR-0410-*.md -SimpleMatch 'stat` off Windows').LineNumber; Select-String -Path Curl.Protocol.File.UnitLibrary/FileProtocolHandler.cs -SimpleMatch 'new Win32SourceLastWriteReader() : new NoRawSourceLastWriteReader()'
```

- Expected: Either the ADR no longer claims a stat read off Windows, or the handler picks a stat-based reader off Windows (no match for the second command).
- Actual: 65, then FileProtocolHandler.cs:369: OperatingSystem.IsWindows() ? new Win32SourceLastWriteReader() : new NoRawSourceLastWriteReader();

## Re-audits

- 2026-10-08 | 2026-10-08_2315.md | reproduces: no | Neither the ADR text 'stat` off Windows' nor the code ternary matches any more. FileProtocolHandler.cs:371-374 now picks Win32SourceLastWriteReader on Windows, PosixSourceLastWriteReader on Linux and macOS, and NoRawSourceLastWriteReader elsewhere. ADR-0410 lines 65-71 describe statx/fgetattrlist off Windows (BL-1790), which matches.
- 2026-10-09 | 2026-10-09_0225.md | reproduces: no | Ran the reproduction, and neither Select-String matched. ADR-0410 now describes statx on Linux and fgetattrlist on macOS (BL-1790, around line 69). FileProtocolHandler.cs:371-374 now picks Win32SourceLastWriteReader on Windows, PosixSourceLastWriteReader (SourceLastWriteReaders.cs:89) on Linux or macOS, and NoRawSourceLastWriteReader otherwise.

## Log

- 2026-10-08: filed proposed.
- 2026-10-08: proposed -> accepted. Stewart: "accept all findings".
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_0225.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-08_2315.md, 2026-10-09_0225.md).
