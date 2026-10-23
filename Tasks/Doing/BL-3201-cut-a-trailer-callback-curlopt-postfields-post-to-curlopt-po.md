---
id: BL-3201
title: Cut a trailer-callback CURLOPT_POSTFIELDS post to CURLOPT_POSTFIELDSIZE
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.PostBody.cs, Curl.Engine.UnitTests/ManagedEasyTransferTests.TrailerPost.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3201 — Cut a trailer-callback CURLOPT_POSTFIELDS post to CURLOPT_POSTFIELDSIZE

## Goal

A `CURLOPT_POSTFIELDS` post with the trailer callback set sends only `CURLOPT_POSTFIELDSIZE` bytes (the whole data for -1 or a size past it), as a plain post does since BL-3190.

## Context

BL-3190 cut the plain post in `HttpRequestOptionsMapping.PostFieldsBodyOf` (ADR-0773); `ManagedEasyTransfer.TrailerPostFieldsBodyOf` still builds its `PostFieldsTrailerStream` over the whole `PostFields` with `fields.Length`. Apply the same cut there.

## Acceptance criteria

- [ ] A test in `Curl.Engine.UnitTests/ManagedEasyTransferTests.TrailerPost.cs` posts `abcdef` with `PostFieldSize` 3 and the trailer callback set, and expects `Content-Length: 3` and body `abc`; build clean, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: c2d196e5.
