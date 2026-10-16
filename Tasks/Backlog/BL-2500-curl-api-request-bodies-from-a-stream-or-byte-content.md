---
id: BL-2500
title: Curl.Api: request bodies from a Stream or byte content
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2499]
touches: [Curl.Api/RequestBodies, Curl.Api.UnitTests/RequestBodies]
requirement: none
created: 2026-10-16
completed:
---
# BL-2500 — Curl.Api: request bodies from a Stream or byte content

## Goal

`CurlRequest.Body` accepts a `Stream` (seekable or not, known length or chunked) or in-memory bytes, and uploads it through the read callback, rewinding through the seek callback.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to the seam read and seek callbacks and the upload and POST options of `EasyTransferOptions` and `EasyHttpOptions`. `Expect: 100-continue` and chunked encoding follow the engine.

## Acceptance criteria

- [ ] Tests cover a seekable stream, a non-seekable stream (chunked), empty content, a read that throws (the transfer fails with `CurlCode.AbortedByCallback`) and a redirect that needs the rewind.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
