---
id: BL-4293
title: OCI extension: push blobs and manifests through upload sessions
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4291, BL-4292]
touches: [Curl.Extension.OciRegistry.UnitLibrary/Push, Curl.Extension.OciRegistry.UnitTests/Push, Curl.Extension.OciRegistry.UnitLibrary/Client, Curl.Extension.OciRegistry.UnitTests/Client/OciRegistryProtocolHandlerTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4293 — OCI extension: push blobs and manifests through upload sessions

## Goal

`curl -T file oci://registry/repo@sha256:...` pushes a blob and `curl -T manifest.json oci://registry/repo:tag` pushes a manifest, with registry token auth.

## Context

Split from BL-4127 (extension 26 of 63; ADR-0999). OCI Distribution Specification 1.1, "Pushing blobs" (`POST /v2/<name>/blobs/uploads/` then `PUT <Location>?digest=<digest>` as a monolithic upload, or the single-POST form; check a blob exists with `HEAD` first) and "Pushing manifests" (`PUT /v2/<name>/manifests/<reference>` with the manifest's `mediaType` as Content-Type). The digest is SHA-256 computed with the BCL. Extends the handler BL-4292 registers; record the push choices in BL-4292's ADR or a new one.

## Acceptance criteria

- [x] Blob push (monolithic upload session, digest verified against the URL's when one is given) and manifest push work as above; unit tests with fake connections cite the specification's sections.
- [x] A 401 during push is answered by the Bearer token flow with a `push,pull` scope.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Plan: `Push/OciPushFlow` does HEAD, then a monolithic `POST /v2/<name>/blobs/uploads/` and `PUT <Location>?digest=` for a blob, and one `PUT /v2/<name>/manifests/<ref>` with the manifest's `mediaType` (default the OCI image manifest's) for a manifest; `Push/OciPushContent` computes the SHA-256 digest (BCL) and checks it against the URL's (exit 25 on mismatch). The handler pushes when `-T` gives an upload. Choices recorded in ADR-1094.
- Touches widened from the two Push folders to also take `Curl.Extension.OciRegistry.UnitLibrary/Client` and `Curl.Extension.OciRegistry.UnitTests/Client/OciRegistryProtocolHandlerTests.cs`: the handler must dispatch to the push, and the token flow was moved from `OciPullFlow` into `Client/OciRegistrySession` so pull and push share it. No task in Doing on work/dark-factory named either.
- Renamed `OciPullException` to `OciTransferException`, since a push throws it too.
- A 401 during a push asks the token for `repository:<name>:push,pull` in place of the challenge's scope; the token goes only to the registry's own scheme, host and port.
- Quality: `Measure-CodeQuality.ps1 -Library Curl.Extension.OciRegistry.UnitLibrary` 100% line, 100% branch, max complexity 10, 0 failing members (after splitting `RequestAuthorizationAsync`, at 12 on the first run). Fast tests green on Windows; tests use no paths, files or OS text, so they are platform-neutral for CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 082e6c1b d29e13e1 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. curl -T pushes blobs (HEAD, monolithic POST/PUT upload, digest checked) and manifests (PUT with their mediaType) to oci:// registries with a push,pull Bearer token
