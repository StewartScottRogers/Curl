---
id: BL-1429
title: Cover HttpContentLength.TryParseItem's empty-item branch to 100% branch coverage
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-03
completed:
---
# BL-1429 — Cover HttpContentLength.TryParseItem's empty-item branch to 100% branch coverage

## Goal

`HttpContentLength.TryParseItem` (Curl.Protocol.Http.UnitLibrary/HttpContentLength.cs:102) is at 100% branch coverage, so Curl.Protocol.Http.UnitLibrary is back at 100% branch.

## Context

- Found by BL-1418's `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary,Curl.Protocol.Http.UnitLibrary,Curl.Console` run on 2026-10-03: the member is at 100% line, 83.33% branch (5 of 6), the library at 99.95% branch. BL-1418 did not change the file; the gap came with BL-1387.
- Likely uncovered: the `item.Length == 0` short-circuit (an empty Content-Length list item, e.g. `Content-Length: 5,,5` or `Content-Length: ,5`); confirm with the coverage report before choosing the test.

## Acceptance criteria

- [ ] A new test in Curl.Protocol.Http.UnitTests drives the uncovered branch and pins the exit code and message real curl 8.21.0 gives for that header (measure with Record-CurlExchange.ps1 if not already pinned).
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 100% line and branch for the library.

## Notes

## Log

- 2026-10-03: Created.
