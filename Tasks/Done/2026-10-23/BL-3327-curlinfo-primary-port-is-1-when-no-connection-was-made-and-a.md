---
id: BL-3327
title: CURLINFO_PRIMARY_PORT is -1 when no connection was made, and a failed redirect keeps the last response's code, IP and port
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ConnectionEndPointRecorder.cs, Curl.Engine.UnitTests/ConnectionEndPointRecorderTests.cs, Libcurl/Info/Longs/EasyInfoReaders.Longs.cs, Curl.Engine.UnitLibrary/EndPointReportingProtocolHandler.cs, Libcurl.UnitTests/Info/Longs/EasyInfoReadersLongsTests.cs, Curl.Engine.UnitTests/EndPointReportingProtocolHandlerTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---# BL-3314 — CURLINFO_PRIMARY_PORT is -1 when no connection was made, and a failed redirect keeps the last response's code, IP and port

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

0x200028 is -1 in libcurl and 0 in Curl for emptyurl, nourl, url-null, badport, file-read and file-dir. get-follow/rftp (302 to ftp://127.0.0.1:1, connect refused): libcurl keeps RESPONSE_CODE 302, PRIMARY_IP 127.0.0.1 and PRIMARY_PORT the server's; Curl answers 0, empty and -1.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Libcurl's PRIMARY_PORT reader answers -1 for 0 (no connection made: setup failures, file://). ConnectionEndPointRecorder.Clear(followingRedirect) keeps the earlier redirect hop's status and end points for a hop failing before it connects; EndPointReportingProtocolHandler passes RedirectsFollowed > 0. Touches widened to the handler and its tests (no other Doing task names them). LibcurlEasyDifferentialTests passed (1 test, 10m38s) against Git ucrt64 libcurl 8.22.0; its output lists no per-case rows. Quality: 0 failing members for Curl.Engine.UnitLibrary and Libcurl. Cross-platform: no OS-specific tests added.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. PRIMARY_PORT is -1 with no connection; a failed redirect hop keeps the earlier hop's code, IP and port
