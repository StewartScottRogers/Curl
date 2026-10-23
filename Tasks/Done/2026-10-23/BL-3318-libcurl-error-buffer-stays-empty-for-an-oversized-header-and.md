---
id: BL-3318
title: Libcurl error buffer stays empty for an oversized header and a gzip trailer write error, and names a bad port as libcurl does
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/Easy/EasyErrorBuffer.cs, Libcurl/Easy/CurlEasy.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---# BL-3318 — Libcurl error buffer stays empty for an oversized header and a gzip trailer write error, and names a bad port as libcurl does

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3310's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel). Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

get*/hugehdr: both return 100 (CURLE_TOO_LARGE), libcurl leaves CURLOPT_ERRORBUFFER empty, Curl writes "A value or data field grew larger than allowed". enc-empty/gziptrail and enc-gzip-only/gziptrail: both return 23, libcurl leaves it empty, Curl writes "Failed writing received data to disk...". badport (http://127.0.0.1:99999/): libcurl "URL rejected: Port number was not a decimal number between 0 and 65535", Curl "URL rejected: Malformed input to a URL function".

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Engine-side fix: ManagedEasyTransfer.Perform.cs now words an unparsable URL with the parser's rejection ("URL rejected: Port number was not a decimal number between 0 and 65535"), and ManagedEasyTransfer.Finish.cs `ErrorTextOf` leaves ErrorText empty when a TooLarge or WriteError perform carries only the code's stock text, so CURLOPT_ERRORBUFFER stays empty as libcurl 8.22.0's. EasyErrorBuffer.cs and CurlEasy.cs needed no change. The partial files ManagedEasyTransfer.Perform.cs and .Finish.cs are read as covered by the touches entry ManagedEasyTransfer.cs. The differential report needs the reference DLL (Integration), not run here; unit tests pin each answer in Curl.Engine.UnitTests. Measure-CodeQuality.ps1 for Curl.Engine.UnitLibrary: 0 failing members. Libcurl.UnitTests pass.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. Perform leaves the error text empty for an oversized header and a gzip trailer write error, and names a bad port as libcurl does
