---
id: BL-3310
title: Port the tester's easytest.py case list to named JSON cases and replay them in the libcurl easy differential runner
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases, Curl.Conformance.Differential.IntegrationTests/LibcurlEasy, Curl.Conformance.Differential.UnitTests/Libcurl/Easy/LibcurlEasyCasesTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3310 — Port the tester's easytest.py case list to named JSON cases and replay them in the libcurl easy differential runner

## Goal

Every case() call in the tester's easytest.py (about 300 named cases) is an embedded JSON case under Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases, and the libcurl easy differential test replays each one, by name, against Curl's Libcurl and libcurl 8.22.0 and writes one report of the differences.

## Context

Split from BL-3303 for size. BL-3237 (ADR-0792) built the easy-handle runner: Libcurl/Easy (LibcurlEasyRoundReader reads e_in.json's round shape: opts, info, reset), IntegrationTests/LibcurlEasy (drivers and LibcurlEasyDifferentialTests) and Cases/LibcurlEasy/EasyRounds.json, which holds only e_in.json, the last case easytest.py wrote. easytest.py (Z:\repos\Curl.difftest\2026-10-22\easytest.py, Python is reference only, never run it) builds its cases with loops and the R() helper (BASE write and header callbacks, URL http://127.0.0.1:18080 + path, the INFO list); expand them by hand or with PowerShell into a JSON array of {name, rounds}. The library's csproj embeds Cases\**\*.json, so a new file needs no project change. Every path easytest.py requests is already a DifferentialRoutes route (checked by BL-3303: /r302, /status/N, /other and /log are pattern, default or control routes). Compare as easytest.py does: err text with 'after N ms' normalised, 0x200028 masked, the wire log with cnonce and response masked.

## Acceptance criteria

- [x] Cases/LibcurlEasyCases holds every easytest.py case() by its easytest.py name, and a unit test checks the file reads with LibcurlEasyRoundReader.
- [x] The Integration test replays every case against both libraries, one run's identical/different counts are recorded under Notes, and each new difference is filed as a task.
- [x] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed library.

## Notes

- Plan: one embedded file, Cases/LibcurlEasyCases/EasyCases.json, a JSON array of {"name", "rounds"} in easytest.py order (467 cases, names unique), expanded from easytest.py's loops and R() helper by a throwaway PowerShell generator (not committed; easytest.py was read, never run). Each case's rounds are read with LibcurlEasyRoundReader.Read; the IntegrationTests' LibcurlEasyNamedCaseReader does that for the runner, and LibcurlEasyCasesTests checks the file reads, the count and some rounds. No production code changed, so the case reader lives beside the runner instead of widening touches into Libcurl/Easy.
- Decided: the runner no longer replays "e_in" (EasyRounds.json) on its own: it is easytest.py's last case, tls-notls-server, now replayed by name, and each copy costs 10 minutes (the 300 s connect timeout in each library). EasyRounds.json stays embedded for LibcurlEasyRoundReaderTests.
- Fixed in the runner: the control port answers one request per connection, so a pooled control connection was reset (the first run died after e_in, the next after get/binary). Control requests now send Connection: close and retry twice on HttpRequestException after checking ThrowIfFaulted; one /reset was retried in the full run. The runner logs each case name before it runs.
- Run 2026-10-23 06:13-06:23 against Git's ucrt64 libcurl-4.dll (8.22.0, Schannel): `cases 519 identical 462 different 57` (467 easytest.py cases, h_round and 51 header cases). Differences by cause, each filed: CONTENT_LENGTH_DOWNLOAD_T - BL-3323; HTTPAUTH_USED - BL-3324; PRIMARY_PORT and failed-redirect info - BL-3327; OS_ERRNO and "Connection was aborted" (incl. header case proxy-connect) - BL-3315; NUM_CONNECTS - BL-3316 (tls-notls-server is BL-3302); SCHEME/PROTOCOL - BL-3317; error buffer text (hugehdr, gziptrail, badport) - BL-3318; CURLOPT_POST alone - BL-3319; chunked upload sizes - BL-3320; low-speed abort info - BL-3321; SSPI text for CURLAUTH_ANY - BL-3322; get*/status/100 is BL-3301. Not a defect: httpver-2 and httpver-2prior differ because this reference build refuses CURLOPT_HTTP_VERSION 3 and 5 (setopt returns 1, no nghttp2), while Curl supports HTTP/2.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Differential.UnitLibrary: 0 failing members (only an embedded JSON file changed there). Curl.Conformance.Differential.UnitTests: 528 passed; build with -warnaserror clean. The new unit test reads only an embedded resource (no paths, OS text or certificates), so it is platform-neutral; the file:///C:/ cases run only in the Integration run, which needs the reference library.

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: 8e79cbd0 4ce8ce3c 0ac4089b.
- 2026-10-23: Doing -> Done. Every easytest.py case is a named embedded JSON case the libcurl easy differential runner replays (519 cases, 462 identical; differences filed as BL-3323 to BL-3322)
