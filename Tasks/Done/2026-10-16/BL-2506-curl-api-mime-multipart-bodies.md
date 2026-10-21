---
id: BL-2506
title: Curl.Api: MIME multipart bodies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2500]
touches: [Curl.Api/Mime, Curl.Api.UnitTests/Mime, Curl.Api/RequestBodies, Curl.Api/Messages, Curl.Api.UnitTests/RequestBodies, Curl.Api.UnitTests/Messages, Curl.Api/CLAUDE.md]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2506 — Curl.Api: MIME multipart bodies

## Goal

A `CurlMultipartContent` builds a multipart body (text fields, files, streams, nested parts, per-part headers, encoders) through `IMimeBuilder` and sends it as a `CurlRequest` body.

## Context

Phase 4 of `Documentation/Planning/Curl-Api-And-libcurl.md`; ADR-0506. `Curl.Api` references `Curl.Api.UnitLibrary` (the seam: `ICurlEngine`, `IEasyTransfer`, `EasyOptions`, `TransferInfo`, callbacks) only until the engine-building task; it never references `Libcurl`. Code lives in `Curl.Api/<Area>` with tests in `Curl.Api.UnitTests/<Area>`, namespace `Curl.Api`, BCL only, native-AOT safe (no reflection). Async with `CancellationToken` everywhere; no `.Result` or `.Wait()`. Maps to `ICurlEngine.CreateMime()`, `IMimeBuilder` and `IMimePartBuilder`; the builder is disposed with the request.

## Acceptance criteria

- [x] Tests with a fake `IMimeBuilder` cover each part kind, headers, encoders, nested parts and disposal; a file that cannot be opened surfaces as `CurlException`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30); the tests use fakes of the seam interfaces, never the network.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Curl.Api/CLAUDE.md` is kept true.

## Notes

- Design (ADR-0637): `CurlMultipartContent(params IEnumerable<CurlMimePart>)` is a third `CurlContent` kind; `CurlMimePart` is an immutable record from the factories `Text`, `Bytes`, `File`, `Stream` and `Multipart`, with `Name`, `FileName`, `ContentType`, `Encoder` and `Headers` set by `with`. A new `IMimeBuilder` is built per send and set as `MimePost` (`Post` off, no post fields); the first failing setter throws `CurlException` (a file that cannot be read is `ReadError`). Every `IDisposable` builder goes in the response body stream's `Owned` and is disposed after the easy handle when the transfer ends (or at once if setup fails).
- Touches widened to `Curl.Api/RequestBodies`, `Curl.Api/Messages`, their test folders and `Curl.Api/CLAUDE.md`: `CurlContent.ApplyTo` had to receive the engine and an owned-handle list, and `SendAsync` disposes them. No other Doing task named those paths (BL-2501 touches only `ResponseBodies`).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Api` reports 0 failing members; Curl.Api.UnitTests 582 passed, 2 skipped (already skipped before); all 41 fast test assemblies passed; `PassingUpstreamCases.txt` unchanged.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing. Criteria at claim: 4d4f4e80 4272a6b9 5393a01b 8738c7f6 ae118507.
- 2026-10-16: Doing -> Done. Curl.Api sends CurlMultipartContent bodies (text, bytes, file, stream, nested parts, headers, encoders) through IMimeBuilder as CURLOPT_MIMEPOST, disposing the builders with the request
