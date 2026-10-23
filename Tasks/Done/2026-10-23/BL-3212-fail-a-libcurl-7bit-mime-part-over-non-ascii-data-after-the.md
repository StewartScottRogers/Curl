---
id: BL-3212
title: Fail a libcurl 7bit MIME part over non-ASCII data after the request starts, as libcurl 8.22.0 does
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-3210]
touches: [Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Core.UnitLibrary/Multipart/MultipartPartEncoder.cs, Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderSevenBitTests.cs, Curl.Engine.UnitLibrary/ManagedEasyTransfer.Parsed.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3212 — Fail a libcurl 7bit MIME part over non-ASCII data after the request starts, as libcurl 8.22.0 does

## Goal

A libcurl part with the `7bit` encoder over non-ASCII data starts the request as libcurl 8.22.0 does and fails with its code (BL-3210's case 2).

## Context

Split from BL-3208. We fail before connecting; upstream starts sending and then fails. The `7bit` refusal is `MultipartPartEncoder` (`CanRefuseData`) and `PassThroughDataEncoding(refusesEightBitData: true)`, read through `EncodedReadStream`, which throws `RequestBodyReadFailedException` at the refused byte; find where `MultipartFormBodyBuilder` checks the data up front for an in-memory part and leave that check to the curl tool, which exits 26 before connecting (measured 2026-10-22, curl 8.22.0, `-F "a=<file;encoder=7bit"`).

## Acceptance criteria

- [x] `Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderSevenBitTests.cs` pins the bytes sent before the failure and the code BL-3210 recorded for case 2, and the tool's early 26 stays.
- [x] Build clean, fast tests pass, Measure-CodeQuality.ps1 0 failing members for the changed libraries.

## Notes

- Added `MultipartFormBodyBuilder.SevenBitRefusalWaitsForSend` (init, default false so the curl tool keeps its early 26); the Engine sets it for libcurl transfers (`ManagedEasyTransfer.Parsed.cs`, added to touches). In-memory and seekable-file 7bit parts then stream and fail at the refused byte. Measured values: Documentation/Product/LibcurlMimeEdgeCaseRecordings.md case 2. Quality: 0 failing members for Curl.Core and Curl.Engine libraries; fast tests green for Core, Engine, Libcurl.

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 2a2dd3bd 491059de.
- 2026-10-22: Doing -> Done. libcurl 7bit parts over non-ASCII data now start the request and fail with 26 at the refused byte; the tool keeps its early 26
