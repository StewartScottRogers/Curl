---
id: BL-4284
title: Extension GCS: scaffold Curl.Extension.GoogleCloudStorage and sign XML API requests with GOOG4-HMAC-SHA256
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.GoogleCloudStorage.UnitLibrary, Curl.Extension.GoogleCloudStorage.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4284 — Extension GCS: scaffold Curl.Extension.GoogleCloudStorage and sign XML API requests with GOOG4-HMAC-SHA256

## Goal

`Curl.Extension.GoogleCloudStorage.UnitLibrary` exists with its tests and signs a Cloud Storage XML API request with an HMAC key (`GOOG4-HMAC-SHA256`) or adds an OAuth 2.0 `Bearer` header.

## Context

Split from BL-4125 (`gs://` extension) for size, as BL-4197 was split from BL-4124 (S3). Follow ADR-0999 and the `new-project` skill; copy the shape of `Curl.Extension.S3.UnitLibrary/Signing` (an extension may not reference `Curl.Protocol.Http` or another extension, so the signer is written here). Specification: Google Cloud Storage XML API, "HMAC keys" and "V4 signing process" (Authorization header form `GOOG4-HMAC-SHA256`, `x-goog-date`, `x-goog-content-sha256`, credential scope `date/region/storage/goog4_request`, region `auto`). One-line `<Folder Name="/Extensions/GoogleCloudStorage/">` in `Curl.slnx` under the marker comment (ADR-1003). The catalogue registration stays with BL-4125.

## Acceptance criteria

- [ ] `Curl.Extension.GoogleCloudStorage.UnitLibrary` and `.UnitTests` exist, listed on one line in `Curl.slnx` under `/Extensions/GoogleCloudStorage/`, and the build's `VerifyExtensionIsolation` passes.
- [ ] A signer returns the `x-goog-date`, `x-goog-content-sha256` and `Authorization` headers for a request at an injected `TimeProvider`'s time; unit tests check the canonical request, string to sign and signature against the "V4 signing process" section, with a fixed key and time.
- [ ] A `Bearer` header is produced from an OAuth 2.0 access token; unit test.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: adf7364c c4387dd4 3e5be813 3180db99 3d836d5e.
