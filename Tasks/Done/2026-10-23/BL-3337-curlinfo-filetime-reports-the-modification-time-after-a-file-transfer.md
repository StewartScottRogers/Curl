---
id: BL-3337
title: CURLINFO_FILETIME reports the modification time after a file:// transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Info/Longs/EasyInfoReaders.Longs.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.cs, Libcurl.UnitTests/Info, Curl.Engine.UnitTests/TransferInfoFromReportTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3337 — CURLINFO_FILETIME reports the modification time after a file:// transfer

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3303's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel) on 2026-10-23. Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

file-read and file-dir (file:// URLs on the tester's disk): FILETIME (0x20000E) is the file's modification time in libcurl (1711956244, 1792339940) and -1 in Curl. Check whether the case sets CURLOPT_FILETIME; libcurl's file:// handler fills the file time from stat.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Filed by BL-3303's differential run. touches names the likely files from a grep; widen it under Notes if the change lands elsewhere.
- Neither case sets CURLOPT_FILETIME: libcurl's file:// handler (file.c) stores the stat time in info.filetime unasked, so TransferInfoFromReport.Of now takes the source time for a file:// effective URL whether or not CURLOPT_FILETIME is set; every other scheme still needs it. This matches measured libcurl 8.22.0, a fact rather than a choice, so no ADR.
- touches widened with Curl.Engine.UnitTests/TransferInfoFromReportTests.cs: the fix is in Curl.Engine.UnitLibrary, whose unit tests pin it, and no Doing task on origin/work/dark-factory names that file. The Libcurl reader needed no change.
- Verified: LibcurlEasyDifferentialTests against Git's ucrt64 libcurl-4.dll no longer lists file-read or file-dir; Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary reports 0 failing members; dotnet build -warnaserror clean and fast tests green. The new tests are platform-neutral (file:///dir/x URLs, no disk access), so CI on Linux and macOS is covered.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. file:// transfers report the source's modification time as CURLINFO_FILETIME without CURLOPT_FILETIME; file-read and file-dir match libcurl 8.22.0
