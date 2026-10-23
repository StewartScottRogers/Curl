---
id: BL-3300
title: Set libcurl's 1<<27 marker bit in curl_header origin from curl_easy_header and curl_easy_nextheader
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/Headers/CurlEasyHeaders.cs, Libcurl.UnitTests/Headers]
requirement: none
created: 2026-10-23
completed:
---
# BL-3300 — Set libcurl's 1<<27 marker bit in curl_header origin from curl_easy_header and curl_easy_nextheader

## Goal

curl_easy_header and curl_easy_nextheader return a curl_header whose origin carries libcurl 8.22.0's 1<<27 marker bit beside the CURLH_* bit, as the reference does.

## Context

BL-3237's replay of the tester's h_in.json: every header case differs only in origin - the reference returns 134217729 (1<<27 | CURLH_HEADER) where Curl's Libcurl returns 1 (libcurl headers.c sets hs->type | (1<<27)). Rerun LibcurlEasyDifferentialTests with CURL_DIFFERENTIAL_REFERENCE_LIBRARY set to see it.

## Acceptance criteria

- [ ] The header cases of LibcurlEasyDifferentialTests show no origin difference.
- [ ] A focused unit test in the fixed library's own .UnitTests project pins it (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed library.

## Notes

- Filed by BL-3237's differential run on 2026-10-23.

## Log

- 2026-10-23: Created.
