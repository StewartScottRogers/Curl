---
id: BL-3337
title: CURLINFO_FILETIME reports the modification time after a file:// transfer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl/Info/Longs/EasyInfoReaders.Longs.cs, Curl.Engine.UnitLibrary/TransferInfoFromReport.cs, Libcurl.UnitTests/Info]
requirement: none
created: 2026-10-23
completed:
---
# BL-3337 — CURLINFO_FILETIME reports the modification time after a file:// transfer

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3303's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel) on 2026-10-23. Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

file-read and file-dir (file:// URLs on the tester's disk): FILETIME (0x20000E) is the file's modification time in libcurl (1711956244, 1792339940) and -1 in Curl. Check whether the case sets CURLOPT_FILETIME; libcurl's file:// handler fills the file time from stat.

## Acceptance criteria

- [ ] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Filed by BL-3303's differential run. touches names the likely files from a grep; widen it under Notes if the change lands elsewhere.

## Log

- 2026-10-23: Created.
