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
completed: 2026-10-29
---
# BL-4284 — Extension GCS: scaffold Curl.Extension.GoogleCloudStorage and sign XML API requests with GOOG4-HMAC-SHA256

## Goal

`Curl.Extension.GoogleCloudStorage.UnitLibrary` exists with its tests and signs a Cloud Storage XML API request with an HMAC key (`GOOG4-HMAC-SHA256`) or adds an OAuth 2.0 `Bearer` header.

## Context

Split from BL-4125 (`gs://` extension) for size, as BL-4197 was split from BL-4124 (S3). Follow ADR-0999 and the `new-project` skill; copy the shape of `Curl.Extension.S3.UnitLibrary/Signing` (an extension may not reference `Curl.Protocol.Http` or another extension, so the signer is written here). Specification: Google Cloud Storage XML API, "HMAC keys" and "V4 signing process" (Authorization header form `GOOG4-HMAC-SHA256`, `x-goog-date`, `x-goog-content-sha256`, credential scope `date/region/storage/goog4_request`, region `auto`). One-line `<Folder Name="/Extensions/GoogleCloudStorage/">` in `Curl.slnx` under the marker comment (ADR-1003). The catalogue registration stays with BL-4125.

## Acceptance criteria

- [x] `Curl.Extension.GoogleCloudStorage.UnitLibrary` and `.UnitTests` exist, listed on one line in `Curl.slnx` under `/Extensions/GoogleCloudStorage/`, and the build's `VerifyExtensionIsolation` passes.
- [x] A signer returns the `x-goog-date`, `x-goog-content-sha256` and `Authorization` headers for a request at an injected `TimeProvider`'s time; unit tests check the canonical request, string to sign and signature against the "V4 signing process" section, with a fixed key and time.
- [x] A `Bearer` header is produced from an OAuth 2.0 access token; unit test.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Type names use the prefix `Gcs` (namespace `Curl.Extension.GoogleCloudStorage.Signing`), the short form the S3 extension's `S3` prefix mirrors; the project name keeps the full name.
- The `Authorization` header separates `Credential`, `SignedHeaders` and `Signature` with ", ", as the "V4 signing process" page shows it (the S3 signer uses ","; both forms are accepted by the XML API's parser).
- Google publishes no HMAC worked example with a signature, so the signature test pins a value derived outside the library by the section's steps (a PowerShell computation of the signing key from `GOOG4`+secret, date, `auto`, `storage`, `goog4_request` and the HMAC of the string to sign); the C# signer reproduced it unchanged.
- `GcsBearerAuthorization` refuses an empty, blank or line-breaking token so a token cannot inject a header (RFC 6750 2.1).
- Measure-CodeQuality.ps1 -Library Curl.Extension.GoogleCloudStorage.UnitLibrary: 100% line, 100% branch, 0 failing members. Full `dotnet build` (with `VerifyExtensionIsolation`) clean; fast tests green (74 assemblies). Nothing platform specific, so CI on Linux and macOS is covered by keeping the change platform-neutral.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: adf7364c c4387dd4 3e5be813 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. GCS extension scaffolded; GOOG4-HMAC-SHA256 signer and Bearer header built and tested, gates met
