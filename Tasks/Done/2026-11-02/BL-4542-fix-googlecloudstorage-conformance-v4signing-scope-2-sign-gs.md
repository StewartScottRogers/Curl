---
id: BL-4542
title: Fix GoogleCloudStorage conformance: V4Signing-Scope-2 (sign gs:// with a named region)
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-4534]
touches: [Curl.Extended.Protocol.GoogleCloudStorage.UnitLibrary/Client/GcsProtocolHandler.cs, Curl.Extended.Protocol.GoogleCloudStorage.UnitTests/Client, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4542 — Fix GoogleCloudStorage conformance: V4Signing-Scope-2 (sign gs:// with a named region)

## Goal

`curl --user ID:SECRET --aws-sigv4 goog4:goog:us-east1:storage gs://bucket/object` signs with the credential scope `<date>/us-east1/storage/goog4_request` and a signing key over `us-east1`, so a bucket checking that region serves it.

## Context

Found by BL-4476: `GcsV4SigningCases.Download_WithHmacKeyAndARegion_SignsWithThatRegionsScope` (`GoogleCloudStorage:V4Signing-Scope-2`) fails with exit 22 / 403 SignatureDoesNotMatch because `GcsProtocolHandler` builds `new GcsHmacKey(user, password)` and so always signs with region `auto`. The region comes from `--aws-sigv4` in curl's own `provider1:provider2:region:service` form (`goog4:goog:<region>:storage`), as the ADR BL-4476 filed records; `GcsHmacKey` already takes a `Region`. Depends on BL-4534, which holds `GcsProtocolHandler.cs`.

## Acceptance criteria

- [x] `GcsV4SigningCases.Download_WithHmacKeyAndARegion_SignsWithThatRegionsScope` passes.
- [x] A focused unit test in `Curl.Extended.Protocol.GoogleCloudStorage.UnitTests` pins the region taken from `--aws-sigv4` (ADR-0482).
- [x] The `GoogleCloudStorage:V4Signing-Scope-2 BL-4538` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; `dotnet build` clean and the fast tests green.

## Notes

- `GcsProtocolHandler.RegionOf` takes the region from the third field of `--aws-sigv4` (`context.Http.AwsSigV4`), as ADR-1191 records; a missing or empty third field keeps `auto`. Focused test: `GcsProtocolHandlerTests.ExecuteAsync_HmacKeyWithAwsSigV4_SignsWithTheRegionInItsThirdField`. Also updated the library's CLAUDE.md line on HMAC signing (documentation of the changed file). Measure-CodeQuality for the GCS library: 0 failing members. Fast tests green; platform-neutral (no paths or OS text).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b8f2907f ed58e9fc 6466a373.
- 2026-11-02: Doing -> Done. gs:// signs with the region --aws-sigv4 goog4:goog:<region>:storage names
