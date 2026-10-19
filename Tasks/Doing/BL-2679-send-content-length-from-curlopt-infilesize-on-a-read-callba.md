---
id: BL-2679
title: Send Content-Length from CURLOPT_INFILESIZE on a read-callback PUT so upstream case 1948 passes
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Libcurl, Libcurl.UnitTests, Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.InFileSize.cs, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2679 — Send Content-Length from CURLOPT_INFILESIZE on a read-callback PUT so upstream case 1948 passes

## Goal

Upstream case 1948 (`CURLOPT_POST after CURLOPT_UPLOAD reusing handle`, run by lib1948) passes through the case runner and holds the ratchet.

## Context

Found by BL-2547, which ported lib1948 (`Curl.Conformance.LibTest.UnitLibrary/Lib1948.cs`): a `PUT` through a `CURLOPT_READFUNCTION` with `CURLOPT_INFILESIZE` 22 and `CURLOPT_HEADER`, then on the same handle `CURLOPT_POST` with `CURLOPT_POSTFIELDS` and `CURLOPT_POSTFIELDSIZE` 22. Curl's first request sends `Transfer-Encoding: chunked` and `Expect: 100-continue` where upstream sends `Content-Length: 22`: `test1948 fails: <verify><protocol> differs at byte 55 (line 4): expected "Content-Length: 22\r\n", got "Transfer-Encoding: chunked\r\n"`. Find where the known upload size is lost (Libcurl's `SetInFileSize` sets `EasyOptions.Transfer.InFileSize`; check how the engine's HTTP upload reads it for a read-callback body), name the project at fault in `touches` before editing, and check the second request (the post must replace the upload) once the first matches. Add a focused unit test in the fixed library's tests (ADR-0482). Run with `CURL_UPSTREAM_CASE=1948`.

## Acceptance criteria

- [ ] A unit test in the fixed library's `.UnitTests` project shows a read-callback `PUT` with a known in-file size sending `Content-Length` and no chunked encoding.
- [ ] Upstream case 1948 passes through the case runner on Windows and is in `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened, and every case on the ratchet still passes.

## Notes

- 2026-10-18: touches narrowed from Curl.Engine.UnitLibrary, Curl.Engine.UnitTests to files, by reading ManagedEasyTransfer upload-size handling (InFileSize); Libcurl and Libcurl.UnitTests left as they are because the setter side was not checked, so it can run beside other Engine tasks.

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 24f1c6d5 3271b0b6 4cf19161.
