---
id: BL-4592
title: Fix OciRegistry conformance: a manifest is pulled by digest (DIST-PullManifests-2)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/OciPullFlow.cs, Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/OciRegistryProtocolHandler.cs, Curl.Extended.Protocol.OciRegistry.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/OciPullReference.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4592 — Fix OciRegistry conformance: a manifest is pulled by digest (DIST-PullManifests-2)

## Goal

curl pulls a manifest by its digest (the Distribution Specification `GET /v2/<name>/manifests/<digest>`), which today `oci://<registry>/<name>@<digest>` cannot reach, since `@digest` always pulls a blob.

## Context

Found by BL-4488: `OciPullCases.ManifestPull_ByDigest_GetsTheManifestUnderItsDigest` stores a manifest under its digest only, and curl asks `/v2/lib/app/blobs/<digest>` and fails with 22 (BLOB_UNKNOWN). The case accepts any request order as long as the manifest request is among them, so the fix may choose the URL form (for instance falling back to `/manifests/` on a blob 404, or a distinct syntax) and records the choice in an ADR beside ADR-1093.

## Acceptance criteria

- [x] `OciRegistry:DIST-PullManifests-2` passes against `OciReferenceRegistry` and its `OciRegistry:DIST-PullManifests-2 BL-4584` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [x] A focused unit test in `Curl.Extended.Protocol.OciRegistry.UnitTests` pins the fix (ADR-0482).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes

- Decided (ADR-1207): an `@digest` pull asks the blob first and, when that is answered 404, `GET /v2/<name>/manifests/<digest>` with the manifest media types; either body is checked against the digest (ADR-1206). When both are 404 the blobs error is reported, so a missing blob reads as before; a non-404 blob failure does not fall back. Kept `@digest` rather than a new syntax since every OCI tool uses it for both.
- Focused unit tests in `OciRegistryProtocolHandlerTests` (fallback, tampered manifest, both missing, manifest error, non-404 no fallback). `OciRegistry:DIST-PullManifests-2` passes and its KnownFailures line is removed.
- `Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.OciRegistry.UnitLibrary`: 0 failing members. Full `dotnet build Curl.slnx -warnaserror` clean and fast tests green on Windows; the change has no path, OS or certificate dependency, so CI on Linux and macOS is covered by it staying platform-neutral.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 5c0d0d92 e1f099c7 8368df41.
- 2026-11-02: Doing -> Done. oci://name@digest falls back to the manifest under the digest when no blob is held (ADR-1207); DIST-PullManifests-2 passes
