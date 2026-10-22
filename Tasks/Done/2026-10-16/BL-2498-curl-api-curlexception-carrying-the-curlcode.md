---
id: BL-2498
title: Curl.Api: CurlException carrying the CurlCode
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Errors, Curl.Api.UnitTests/Errors, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2498 — Curl.Api: CurlException carrying the CurlCode

## Goal

`CurlException` carries the `CurlCode` and the error text, and one function maps a failed transfer to it, so every failure in `Curl.Api` throws the same type.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. The error text for a code lives in `Curl.Console/CurlEasyErrorText.cs` today and moves to `Libcurl` (BL-2450); `Curl.Api` may not reference either, so read what the seam (`CurlCodeConversion`) offers and decide, recording it under Notes, whether the text comes from the engine error buffer or from a table in `Curl.Api`; measure curl 8.22.0 wording before pinning any text.

## Acceptance criteria

- [x] `CurlException.Code` returns the `CurlCode`; `CurlCode.Ok` never throws; every `CurlCode` value maps to a message; tests cover each value.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Text source (ADR-0630): `Curl.Api` may not reference `Libcurl`, and the seam offers only `CurlCodeConversion` and `IEasyTransfer.ErrorText`, so `Curl.Api/Errors/CurlCodeText.cs` holds its own `curl_easy_strerror` table. Measured on 2026-10-16 by calling `curl_easy_strerror(0..102)` in curl 8.22.0's `libcurl-4.dll` (mingw, Schannel, `/ucrt64/bin`) from a throwaway C# app outside the repo; the text matches `Libcurl`'s 8.21.0 table, obsolete numbers and 102 give `Unknown error`.
- `CurlException.ThrowIfFailed(CurlCode, string errorText)` is the one mapping function: `Ok` returns; otherwise the message is the error buffer when not empty, else the strerror text. It takes the error text rather than an `IEasyTransfer`, so callers pass `transfer.ErrorText` and tests need no fake handle. `new CurlException(Ok, ...)` throws `ArgumentOutOfRangeException`.
- Added `Curl.Api/CLAUDE.md` to `touches`: the acceptance criteria require keeping it true, and no task in Doing names it.
- Measure-CodeQuality.ps1 -Library Curl.Api: 100% line, 100% branch, 0 failing members. Full fast run: 0 failures; Curl.Api.UnitTests 440 passed, 2 skipped (OS-conditioned). PassingUpstreamCases.txt unchanged.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Curl.Api's CurlException carries the CurlCode; ThrowIfFailed maps every failed code to it with the error buffer or curl 8.22.0's strerror text
