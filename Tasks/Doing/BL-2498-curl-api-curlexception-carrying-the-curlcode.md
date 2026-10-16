---
id: BL-2498
title: Curl.Api: CurlException carrying the CurlCode
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Api/Errors, Curl.Api.UnitTests/Errors]
requirement: none
created: 2026-10-16
completed:
---
# BL-2498 — Curl.Api: CurlException carrying the CurlCode

## Goal

`CurlException` carries the `CurlCode` and the error text, and one function maps a failed transfer to it, so every failure in `Curl.Api` throws the same type.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. The error text for a code lives in `Curl.Console/CurlEasyErrorText.cs` today and moves to `Libcurl` (BL-2450); `Curl.Api` may not reference either, so read what the seam (`CurlCodeConversion`) offers and decide, recording it under Notes, whether the text comes from the engine error buffer or from a table in `Curl.Api`; measure curl 8.22.0 wording before pinning any text.

## Acceptance criteria

- [ ] `CurlException.Code` returns the `CurlCode`; `CurlCode.Ok` never throws; every `CurlCode` value maps to a message; tests cover each value.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
