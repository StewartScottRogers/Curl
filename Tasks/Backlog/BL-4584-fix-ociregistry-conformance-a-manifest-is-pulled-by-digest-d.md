---
id: BL-4584
title: Fix OciRegistry conformance: a manifest is pulled by digest (DIST-PullManifests-2)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/OciPullFlow.cs, Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/OciRegistryProtocolHandler.cs, Curl.Extended.Protocol.OciRegistry.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Extended.Protocol.OciRegistry.UnitLibrary/Client/OciPullReference.cs]
requirement: none
created: 2026-11-02
completed:
---
# BL-4584 — Fix OciRegistry conformance: a manifest is pulled by digest (DIST-PullManifests-2)

## Goal

curl pulls a manifest by its digest (the Distribution Specification `GET /v2/<name>/manifests/<digest>`), which today `oci://<registry>/<name>@<digest>` cannot reach, since `@digest` always pulls a blob.

## Context

Found by BL-4488: `OciPullCases.ManifestPull_ByDigest_GetsTheManifestUnderItsDigest` stores a manifest under its digest only, and curl asks `/v2/lib/app/blobs/<digest>` and fails with 22 (BLOB_UNKNOWN). The case accepts any request order as long as the manifest request is among them, so the fix may choose the URL form (for instance falling back to `/manifests/` on a blob 404, or a distinct syntax) and records the choice in an ADR beside ADR-1093.

## Acceptance criteria

- [ ] `OciRegistry:DIST-PullManifests-2` passes against `OciReferenceRegistry` and its `OciRegistry:DIST-PullManifests-2 BL-4584` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`.
- [ ] A focused unit test in `Curl.Extended.Protocol.OciRegistry.UnitTests` pins the fix (ADR-0482).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, `dotnet test --filter "TestCategory!=Integration"` is green, and the library stays at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 (`Measure-CodeQuality.ps1`).

## Notes


## Log

- 2026-11-02: Created.
