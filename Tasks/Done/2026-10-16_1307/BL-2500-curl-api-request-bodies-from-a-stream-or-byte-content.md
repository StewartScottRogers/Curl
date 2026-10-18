---
id: BL-2500
title: Curl.Api: request bodies from a Stream or byte content
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2499]
touches: [Curl.Api/RequestBodies, Curl.Api.UnitTests/RequestBodies, Curl.Api/Messages, Curl.Api.UnitTests/Messages/CurlRequestTests.cs, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2500 — Curl.Api: request bodies from a Stream or byte content

## Goal

`CurlRequest.Body` accepts a `Stream` (seekable or not, known length or chunked) or in-memory bytes, and uploads it through the read callback, rewinding through the seek callback.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to the seam read and seek callbacks and the upload and POST options of `EasyTransferOptions` and `EasyHttpOptions`. `Expect: 100-continue` and chunked encoding follow the engine.

## Acceptance criteria

- [x] Tests cover a seekable stream, a non-seekable stream (chunked), empty content, a read that throws (the transfer fails with `CurlCode.AbortedByCallback`) and a redirect that needs the rewind.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Design (ADR-0634): `CurlRequest.Body` is now `CurlContent?` (`Curl.Api/RequestBodies`). Bytes stay
  `CURLOPT_POSTFIELDS`; `CurlContent.FromStream(stream, length?)` posts with no post fields, so the
  engine reads through `StreamUpload.Read`, and rewinds through `StreamUpload.Seek` to the stream's
  position at send time. Size: the given length, else a seekable stream's remaining length, else -1
  (chunked). A throwing read returns `CURL_READFUNC_ABORT` (`AbortedByCallback`); a throwing seek
  `CURL_SEEKFUNC_FAIL`; a non-seekable stream `CURL_SEEKFUNC_CANTSEEK`.
- Implicit conversions from `byte[]` and `ReadOnlyMemory<byte>` keep `Body = new byte[] { … }` compiling.
- Touches widened to `Curl.Api/Messages` (`CurlRequest.Body`'s type and one line in
  `CurlRequestOptions.Apply`), `Curl.Api.UnitTests/Messages/CurlRequestTests.cs` (it read
  `Body.Value`) and `Curl.Api/CLAUDE.md` (an acceptance criterion): the body type lives on
  `CurlRequest`. No task in Doing on `origin/work/dark-factory` names them.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` 100% line, 100% branch, 0 failing members;
  fast tests all green (Curl.Api.UnitTests 509 passed, 2 skipped by platform).
- Default taken: the original exception of a failed read is not carried on `CurlException` (it has
  no inner-exception constructor, and `Errors/` is outside touches); recorded in ADR-0634.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: f0579c35 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. CurlRequest.Body takes bytes or a Stream (CurlContent), uploaded through the read callback and rewound through the seek callback
