---
id: BL-2499
title: Curl.Api: CurlRequest and CurlResponse records
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2496, BL-2498]
touches: [Curl.Api/Messages, Curl.Api.UnitTests/Messages, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2499 — Curl.Api: CurlRequest and CurlResponse records

## Goal

A `CurlRequest` (method, URL, headers, body) and a `CurlResponse` (status code, reason, version, headers, trailers, `TransferInfo`) exist as immutable records, with `CurlClient.SendAsync(CurlRequest, CancellationToken)` performing one transfer through a fake engine and returning the response with an empty body stream.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Header casing and duplicates are preserved as received (the header API origins). The body is a `Stream`, filled by the next two tasks; this task returns an empty body stream.

## Acceptance criteria

- [x] Tests with a fake `IEasyTransfer` show the request method, URL and headers reach `EasyOptions` and the response status, version and headers come from `TransferInfo` and the header callback.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Design in ADR-0632: `CurlRequest(HttpMethod, string url)` with `Headers` and an in-memory
  `Body` (BL-2500 widens it to streams); `CurlResponse` with status, reason, version, headers,
  trailers, `Info` and `Body = Stream.Null` (BL-2501 streams it). Method maps as the tool maps
  `-I`/`-d`/`-X`; an empty header value is sent as `Name;`.
- Code in `Curl.Api/Messages` (namespace `Curl.Api`): the two records, `CurlRequestOptions`
  (request to `EasyOptions`), `ResponseHeaderCollector` (header callback: a status line resets,
  fields after the blank line are trailers) and the partial `CurlClient.SendAsync`.
- Tests in `Curl.Api.UnitTests/Messages` use their own `ScriptedEasyTransfer` and
  `ScriptedCurlEngine` rather than the `Client/` fakes, which are outside this task's `touches`.
- Added `Curl.Api/CLAUDE.md` to `touches` (the acceptance criteria require keeping it true); no
  task in Doing on `origin/work/dark-factory` names it.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` - 100% line, 100% branch, 33 members,
  0 failing, worst CRAP 8 (first run had `Apply` at complexity 12 and `VersionOf` at 87.5% branch;
  split `Apply` and made `VersionOf` a table lookup). Curl.Api.UnitTests: 488 passed, 2 skipped
  (platform). Full fast run: no failures. `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. CurlClient.SendAsync performs a CurlRequest and returns a CurlResponse with status, reason, version, headers and trailers
