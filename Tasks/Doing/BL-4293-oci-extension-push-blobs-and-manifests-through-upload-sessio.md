---
id: BL-4293
title: OCI extension: push blobs and manifests through upload sessions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4291, BL-4292]
touches: [Curl.Extension.OciRegistry.UnitLibrary/Push, Curl.Extension.OciRegistry.UnitTests/Push]
requirement: none
created: 2026-10-29
completed:
---
# BL-4293 — OCI extension: push blobs and manifests through upload sessions

## Goal

`curl -T file oci://registry/repo@sha256:...` pushes a blob and `curl -T manifest.json oci://registry/repo:tag` pushes a manifest, with registry token auth.

## Context

Split from BL-4127 (extension 26 of 63; ADR-0999). OCI Distribution Specification 1.1, "Pushing blobs" (`POST /v2/<name>/blobs/uploads/` then `PUT <Location>?digest=<digest>` as a monolithic upload, or the single-POST form; check a blob exists with `HEAD` first) and "Pushing manifests" (`PUT /v2/<name>/manifests/<reference>` with the manifest's `mediaType` as Content-Type). The digest is SHA-256 computed with the BCL. Extends the handler BL-4292 registers; record the push choices in BL-4292's ADR or a new one.

## Acceptance criteria

- [ ] Blob push (monolithic upload session, digest verified against the URL's when one is given) and manifest push work as above; unit tests with fake connections cite the specification's sections.
- [ ] A 401 during push is answered by the Bearer token flow with a `push,pull` scope.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 082e6c1b d29e13e1 3180db99 3d836d5e.
