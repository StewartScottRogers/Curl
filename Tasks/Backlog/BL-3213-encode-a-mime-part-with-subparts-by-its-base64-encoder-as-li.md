---
id: BL-3213
title: Encode a MIME part with subparts by its base64 encoder as libcurl 8.22.0 does
priority: Low
assignee: Claude
pipeline: direct
depends-on: [BL-3210]
touches: [Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderEncodedSubpartsTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3213 — Encode a MIME part with subparts by its base64 encoder as libcurl 8.22.0 does

## Goal

A part with `curl_mime_subparts` and `curl_mime_encoder(part, "base64")` gives libcurl 8.22.0's body byte for byte (BL-3210's case 3).

## Context

Split from BL-3208. Stewart's harness gave 449 bytes upstream, 297 ours: libcurl encodes the nested multipart (its headers and boundaries) with the part's encoder and adds `Content-Transfer-Encoding: base64`; our `MultipartFormBodyBuilder` appears to skip the encoder for a `MultipartFormPartKind.Multipart` part. The curl tool applies no encoder to a `=(` part (measured 2026-10-22), so only parts given an encoder through libcurl change.

## Acceptance criteria

- [ ] `Curl.Core.UnitTests/Multipart/MultipartFormBodyBuilderEncodedSubpartsTests.cs` pins the body BL-3210 recorded for case 3 byte for byte (boundaries fixed by the test).
- [ ] Build clean, fast tests pass, Measure-CodeQuality.ps1 0 failing members for the changed libraries.

## Notes

## Log

- 2026-10-22: Created.
