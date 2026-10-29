---
id: BL-4197
title: Extension: S3 SigV4 request signer
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.S3.UnitLibrary, Curl.Extension.S3.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4197 — Extension: S3 SigV4 request signer

## Goal

`Curl.Extension.S3.UnitLibrary` exists with a signer that adds AWS Signature Version 4 headers (`x-amz-date`, `x-amz-content-sha256`, `Authorization`) to an S3 request, exactly as the Amazon S3 API reference "Authenticating Requests (AWS Signature Version 4)" says.

## Context

Split from BL-4124 for size (its Notes ask for "wire format, then the client flow, then the options"). Extension rules: ADR-0999, ADR-1003 (projects join by wildcard reference and one single-line `<Folder Name="/Extensions/S3/">` entry in `Curl.slnx` under the marker comment). Use the `new-project` skill; BL-4175 (ServerSentEvents) is the model. References only `Curl.Extension.Abstractions` and `Curl.Protocol.Abstractions` if needed; never `Curl.Protocol.Http` (its `--aws-sigv4` code may not be shared). BCL only: `SHA256`, `HMACSHA256`. Inputs: method, path, query parameters, headers, payload hash (or `UNSIGNED-PAYLOAD`), access key, secret key, region, service `s3`, an injected time. Canonical URI encodes each path segment once (S3 does not double-encode), canonical query sorted by encoded name, signed headers lower-cased and sorted. Put signing code in `Signing/`; BL-4198 and BL-4199 add `Xml/` and `Http/` beside it.

## Acceptance criteria

- [ ] `Curl.Extension.S3.UnitLibrary` and `Curl.Extension.S3.UnitTests` exist in `/Extensions/S3/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [ ] Its signer reproduces the signatures of the S3 reference's own examples in "Signature Calculations for the Authorization Header: Transferring Payload in a Single Chunk" (GET Object with Range, PUT Object, GET Bucket Lifecycle, GET Bucket list objects), with unit tests citing the section.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
