---
id: BL-3301
title: Keep a 1xx-only response's headers: header callback and curl_easy_header after a failed /status/100 transfer
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Libcurl/Headers]
requirement: none
created: 2026-10-23
completed:
---
# BL-3301 — Keep a 1xx-only response's headers: header callback and curl_easy_header after a failed /status/100 transfer

## Goal

A transfer whose server sends only a 1xx response and then closes (/status/100) passes the 1xx header block to the header callback and leaves it for curl_easy_header (CURLHE_MISSING, 2, for an absent name), as libcurl 8.22.0 does.

## Context

BL-3237's replay: /status/100 and '/status/100 follow' both end rc=8 in both libraries, but the reference's header callback got 'HTTP/1.1 100 Status' with its Date, Content-Type and Content-Length lines, and its curl_easy_header answers 2 (CURLHE_MISSING); Curl's Libcurl delivered no header bytes and answers 3 (CURLHE_NOHEADERS). Where it lands in Curl.Engine.UnitLibrary is not yet known.

## Acceptance criteria

- [ ] The /status/100 header cases of LibcurlEasyDifferentialTests show no difference.
- [ ] A focused unit test in the fixed library's own .UnitTests project pins it (ADR-0482).
- [ ] Projects build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 reports 0 failing members for the changed library.

## Notes

- Filed by BL-3237's differential run on 2026-10-23.

## Log

- 2026-10-23: Created.
