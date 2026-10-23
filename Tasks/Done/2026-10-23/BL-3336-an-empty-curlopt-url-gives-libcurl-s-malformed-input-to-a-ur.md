---
id: BL-3336
title: An empty CURLOPT_URL gives libcurl's 'Malformed input to a URL function' error buffer text
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlRejectionMessages.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlRejectionMessagesTests.cs, Libcurl/Misc/ErrorText/CurlErrorText.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Protocol.Abstractions.UnitTests/CurlUrlRejectionTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3336 — An empty CURLOPT_URL gives libcurl's 'Malformed input to a URL function' error buffer text

## Goal

Curl's Libcurl gives the same answer as libcurl 8.22.0 in the easytest.py cases named below.

## Context

Found by BL-3303's run of the easytest.py case list through Curl.Conformance.Differential.IntegrationTests/LibcurlEasy (LibcurlEasyDifferentialTests, CURL_DIFFERENTIAL_REFERENCE_LIBRARY = Git's ucrt64 libcurl-4.dll, 8.22.0 Schannel) on 2026-10-23. Each case is named in Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases/EasyCases.json; info ids are CURLINFO numbers in hex, reference value first.

emptyurl (CURLOPT_URL ""): both return CURLE_URL_MALFORMAT (3), but libcurl's error buffer reads 'URL rejected: Malformed input to a URL function' and Curl's 'URL rejected: No host present'.

## Acceptance criteria

- [x] The named cases are identical in LibcurlEasyDifferentialTests' report, and a focused unit test pins each answer (ADR-0482).
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed libraries.

## Notes

- Filed by BL-3303's differential run. touches names the likely files from a grep; widen it under Notes if the change lands elsewhere.
- Widened touches to CurlUrlParser.cs and CurlUrlRejectionTests.cs: the message table was already right; the empty URL fell through to the empty-authority check (NoHost). No Doing task on origin/work/dark-factory held either file.
- Fix: CurlUrlParser.Parse rejects empty text as MalformedInput, as libcurl 8.22.0 does for CURLOPT_URL "" (measured by the differential run: CURLE_URL_MALFORMAT, "URL rejected: Malformed input to a URL function"). The curl tool never reaches this (it refuses a blank argument with exit 2), so only libcurl and the URL API see it. Matching upstream follows a standing rule, so no ADR was needed. The check moved into IsMalformedInput to keep Parse at complexity 10.
- Verified: LibcurlEasyDifferentialTests against Git ucrt64 libcurl-4.dll 8.22.0 no longer lists emptyurl. The focused DataRow("") in CurlUrlRejectionTests pins the answer. Build is clean with -warnaserror, and all fast tests pass. Measure-CodeQuality reports 0 failing members for Curl.Protocol.Abstractions.UnitLibrary (100/100). The change is platform-neutral.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: f5b2f496 db93e725.
- 2026-10-23: Doing -> Done. An empty CURLOPT_URL gives libcurl's 'URL rejected: Malformed input to a URL function'
