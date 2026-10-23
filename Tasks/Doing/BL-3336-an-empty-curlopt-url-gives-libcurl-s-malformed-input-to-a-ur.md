---
id: BL-3336
title: An empty CURLOPT_URL gives libcurl's 'Malformed input to a URL function' error buffer text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlRejectionMessagesTests.cs, Libcurl/Misc/ErrorText/CurlErrorText.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3336 — An empty CURLOPT_URL gives libcurl's 'Malformed input to a URL function' error buffer text

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3303's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel) on 2026-10-23. Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

emptyurl (CURLOPT_URL ""): both return CURLE_URL_MALFORMAT (3), but libcurl's error buffer reads 'URL rejected: Malformed input to a URL function' and Curl's 'URL rejected: No host present'.

## Acceptance criteria

- [ ] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Filed by BL-3303's differential run. touches names the likely files from a grep; widen it under Notes if the change lands elsewhere.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
