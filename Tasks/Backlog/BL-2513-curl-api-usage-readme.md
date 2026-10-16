---
id: BL-2513
title: Curl.Api: usage README
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-2511, BL-2512, BL-2508, BL-2497]
touches: [Curl.Api/README.md, Curl.Api/CLAUDE.md, Curl.Api.UnitTests/Readme]
requirement: none
created: 2026-10-16
completed:
---
# BL-2513 — Curl.Api: usage README

## Goal

A `Curl.Api/README.md` shows, with code that compiles, how to create a client, send a request and stream a body, set options, cancel, report progress, upload multipart, use a WebSocket, run concurrent transfers and use `CurlHttpMessageHandler` with `HttpClient`.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Every sample is a code block that a test in `Curl.Api.UnitTests` also runs (against a fake engine), so the README cannot drift. State plainly what differs from `HttpClient`.

## Acceptance criteria

- [ ] Every code block in the README has a test of the same sample that passes.
- [ ] The README names every public type of `Curl.Api` and says what it does; no statement in it is untrue of the code.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.

## Notes

## Log

- 2026-10-16: Created.
