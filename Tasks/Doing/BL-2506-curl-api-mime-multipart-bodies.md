---
id: BL-2506
title: Curl.Api: MIME multipart bodies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2500]
touches: [Curl.Api/Mime, Curl.Api.UnitTests/Mime]
requirement: none
created: 2026-10-16
completed:
---
# BL-2506 — Curl.Api: MIME multipart bodies

## Goal

A `CurlMultipartContent` builds a multipart body (text fields, files, streams, nested parts, per-part headers, encoders) through `IMimeBuilder` and sends it as a `CurlRequest` body.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to `ICurlEngine.CreateMime()`, `IMimeBuilder` and `IMimePartBuilder`; the builder is disposed with the request.

## Acceptance criteria

- [ ] Tests with a fake `IMimeBuilder` cover each part kind, headers, encoders, nested parts and disposal; a file that cannot be opened surfaces as `CurlException`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [ ] `Curl.Api/CLAUDE.md` is kept true.

## Notes

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 4d4f4e80 4272a6b9 5393a01b 8738c7f6 ae118507.
