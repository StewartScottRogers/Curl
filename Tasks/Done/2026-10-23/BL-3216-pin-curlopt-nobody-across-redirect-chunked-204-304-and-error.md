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
completed: 2026-10-22
---
# BL-3216 — Pin CURLOPT_NOBODY across redirect, chunked, 204, 304 and error-status responses

## Goal

Under `CURLOPT_NOBODY` the write callback gets zero bytes and the header callback gets upstream's headers for every response shape, not only a plain 200.

## Context

Stewart reported on 2026-10-22 (prioritised findings list, item 19 follow-up). BL-3199 (Done) fixed header bytes reaching the write callback under NOBODY and tested HTTP and FTP with and without a header callback. The tester's list asks for NOBODY across GET, redirect (with FOLLOWLOCATION), chunked, 204, 304 and an error status (with and without FAILONERROR). Add those, fixing the engine if any shape still leaks; add the engine file to `touches` if so.

Measure upstream libcurl 8.22.0 before pinning values; tests use fakes or loopback only and pass on Windows, Linux and macOS.

## Acceptance criteria

- [x] `Curl.Engine.UnitTests/NobodyResponseShapeTests.cs` covers GET, a followed 302, a chunked 200, 204, 304 and a 404 (with and without FAILONERROR), each expecting zero write-callback bytes, upstream's header-callback bytes, RESPONSE_CODE and exit code.
- [x] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- 2026-10-22 (lane 4): Ran the `feature` pipeline directly, with no architect stage: the task only adds tests and the engine needed no fix, so there was nothing to plan. Measured curl 8.22.0 with Record-CurlExchange.ps1 (`-I`, `-I -L`, `-I -f`, and `-I --no-include -f -D` for the 404). Every shape writes only its heads (both hops of a followed 302; a 404 head even under -f, exit 22) and no body. All seven tests passed against the engine as it stands, so `touches` stayed as it was. Build clean with -warnaserror; Curl.Engine.UnitTests fast tests 2306 passed, 5 skipped (OS-conditioned); Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary reported 0 failing members. The tests use the scripted connector only, with no paths and no OS text, so they are platform-neutral.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 0934129b 3639a5e6.
- 2026-10-22: Doing -> Done. NOBODY tests pin zero write bytes, upstream heads, RESPONSE_CODE and exit code for GET, followed 302, chunked, 204, 304 and 404 with and without FAILONERROR
