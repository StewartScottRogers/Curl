---
id: BL-2499
title: Curl.Api: CurlRequest and CurlResponse records
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2496, BL-2498]
touches: [Curl.Api/Messages, Curl.Api.UnitTests/Messages]
requirement: none
created: 2026-10-16
completed:
---
# BL-2499 — Curl.Api: CurlRequest and CurlResponse records

## Goal

A `CurlRequest` (method, URL, headers, body) and a `CurlResponse` (status code, reason, version, headers, trailers, `TransferInfo`) exist as immutable records, with `CurlClient.SendAsync(CurlRequest, CancellationToken)` performing one transfer through a fake engine and returning the response with an empty body stream.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Header casing and duplicates are preserved as received (the header API origins). The body is a `Stream`, filled by the next two tasks; this task returns an empty body stream.

## Acceptance criteria

- [ ] Tests with a fake `IEasyTransfer` show the request method, URL and headers reach `EasyOptions` and the response status, version and headers come from `TransferInfo` and the header callback.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
