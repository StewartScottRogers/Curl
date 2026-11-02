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
completed:
---
# BL-4542 — Fix GoogleCloudStorage conformance: V4Signing-Scope-2 (sign gs:// with a named region)

## Goal

`curl --user ID:SECRET --aws-sigv4 goog4:goog:us-east1:storage gs://bucket/object` signs with the credential scope `<date>/us-east1/storage/goog4_request` and a signing key over `us-east1`, so a bucket checking that region serves it.

## Context

Found by BL-4476: `GcsV4SigningCases.Download_WithHmacKeyAndARegion_SignsWithThatRegionsScope` (`GoogleCloudStorage:V4Signing-Scope-2`) fails with exit 22 / 403 SignatureDoesNotMatch because `GcsProtocolHandler` builds `new GcsHmacKey(user, password)` and so always signs with region `auto`. The region comes from `--aws-sigv4` in curl's own `provider1:provider2:region:service` form (`goog4:goog:<region>:storage`), as the ADR BL-4476 filed records; `GcsHmacKey` already takes a `Region`. Depends on BL-4534, which holds `GcsProtocolHandler.cs`.

## Acceptance criteria

- [ ] `GcsV4SigningCases.Download_WithHmacKeyAndARegion_SignsWithThatRegionsScope` passes.
- [ ] A focused unit test in `Curl.Extended.Protocol.GoogleCloudStorage.UnitTests` pins the region taken from `--aws-sigv4` (ADR-0482).
- [ ] The `GoogleCloudStorage:V4Signing-Scope-2 BL-4538` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; `dotnet build` clean and the fast tests green.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b8f2907f ed58e9fc 6466a373.
