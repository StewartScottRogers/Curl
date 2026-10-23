---
id: BL-3216
title: Pin CURLOPT_NOBODY across redirect, chunked, 204, 304 and error-status responses
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests/NobodyResponseShapeTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3216 — Pin CURLOPT_NOBODY across redirect, chunked, 204, 304 and error-status responses

## Goal

Under `CURLOPT_NOBODY` the write callback gets zero bytes and the header callback gets upstream's headers for every response shape, not only a plain 200.

## Context

Stewart reported on 2026-10-22 (prioritised findings list, item 19 follow-up). BL-3199 (Done) fixed header bytes reaching the write callback under NOBODY and tested HTTP and FTP with and without a header callback. The tester's list asks for NOBODY across GET, redirect (with FOLLOWLOCATION), chunked, 204, 304 and an error status (with and without FAILONERROR). Add those, fixing the engine if any shape still leaks; add the engine file to `touches` if so.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [ ] `Curl.Engine.UnitTests/NobodyResponseShapeTests.cs` covers GET, a followed 302, a chunked 200, 204, 304 and a 404 (with and without FAILONERROR), each expecting zero write-callback bytes, upstream's header-callback bytes, RESPONSE_CODE and exit code.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
