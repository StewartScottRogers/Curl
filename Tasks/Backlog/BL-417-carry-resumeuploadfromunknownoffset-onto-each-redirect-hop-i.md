---
id: BL-417
title: Carry ResumeUploadFromUnknownOffset onto each redirect hop in RedirectFollower
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-351]
touches: [Curl.Core.UnitLibrary, Curl.Core.UnitTests]
requirement: none
created: 2026-09-27
completed:
---
# BL-417 — Carry ResumeUploadFromUnknownOffset onto each redirect hop in RedirectFollower

## Goal

A redirect hop that keeps the `-T` upload carries `ITransferContext.ResumeUploadFromUnknownOffset` from the first hop, as it already carries `ResumeFrom`.

## Context

- BL-351 added `ResumeUploadFromUnknownOffset` to `ITransferContext` (ADR-0087). `RedirectFollower.NextHop` in `Curl.Core.UnitLibrary` builds each hop's `TransferContext` field by field and does not copy it, so a `-L -C - -T f.txt` PUT redirected by a 307 or 308 loses its `Content-Range` on the second hop.
- Where the hop drops the upload (`bodyDropped`), the flag has no upload to act on; copying it is harmless, but setting it only when the upload is kept says what it does.
- `RedirectFollowerTests` already pins that `ResumeFrom` is copied (the test around line 700).

## Acceptance criteria

- [ ] A `RedirectFollowerTests` test pins that the second hop's `ResumeUploadFromUnknownOffset` equals the first hop's when the upload is kept, and is `false` when the hop drops the upload.
- [ ] `dotnet build -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Core.UnitLibrary`.

## Notes

## Log

- 2026-09-27: Created.
