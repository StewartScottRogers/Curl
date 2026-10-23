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
completed:
---
# BL-3310 — Port the tester's easytest.py case list to named JSON cases and replay them in the libcurl easy differential runner

## Goal

Every case() call in the tester's easytest.py (about 300 named cases) is an embedded JSON case under Curl.Conformance.Differential.UnitLibrary/Cases/LibcurlEasyCases, and the libcurl easy differential test replays each one, by name, against Curl's Libcurl and libcurl 8.22.0 and writes one report of the differences.

## Context

Split from BL-3303 for size. BL-3237 (ADR-0792) built the easy-handle runner: Libcurl/Easy (LibcurlEasyRoundReader reads e_in.json's round shape: opts, info, reset), IntegrationTests/LibcurlEasy (drivers and LibcurlEasyDifferentialTests) and Cases/LibcurlEasy/EasyRounds.json, which holds only e_in.json, the last case easytest.py wrote. easytest.py (Z:\repos\Curl.difftest\2026-10-22\easytest.py, Python is reference only, never run it) builds its cases with loops and the R() helper (BASE write and header callbacks, URL http://127.0.0.1:18080 + path, the INFO list); expand them by hand or with PowerShell into a JSON array of {name, rounds}. The library's csproj embeds Cases\**\*.json, so a new file needs no project change. Every path easytest.py requests is already a DifferentialRoutes route (checked by BL-3303: /r302, /status/N, /other and /log are pattern, default or control routes). Compare as easytest.py does: err text with 'after N ms' normalised, 0x200028 masked, the wire log with cnonce and response masked.

## Acceptance criteria

- [ ] Cases/LibcurlEasyCases holds every easytest.py case() by its easytest.py name, and a unit test checks the file reads with LibcurlEasyRoundReader.
- [ ] The Integration test replays every case against both libraries, one run's identical/different counts are recorded under Notes, and each new difference is filed as a task.
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed library.

## Notes

## Log

- 2026-10-23: Created.
