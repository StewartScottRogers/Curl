---
id: BL-4125
title: Extension: Read and write Google Cloud Storage objects with gs://
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4091, BL-4093, BL-4092, BL-4284, BL-4285, BL-4286]
touches: [Curl.Extension.GoogleCloudStorage.UnitLibrary, Curl.Extension.GoogleCloudStorage.UnitTests, Curl.Extension.Composition.UnitLibrary/ExtensionCatalogue.cs, Curl.slnx, Documentation/Planning/Decisions/README.md, Curl.Extension.Composition.UnitLibrary/ExtensionDescriptionCatalogue.cs, Curl.Console.UnitTests/CurlCompositionTests.cs]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4125 — Extension: Read and write Google Cloud Storage objects with gs://

## Goal

Curl provides `gs://bucket/object` URLs with OAuth bearer or HMAC keys, as the extension `Curl.Extension.GoogleCloudStorage.UnitLibrary`.

## Context

From the extensions list Stewart chose in full on 2026-10-29. Extension rules: ADR-0999 and CLAUDE.md "Extensions". Extension 24 of 63. Specification: Google Cloud Storage JSON and XML API. It is tested against that specification, not against curl, and never changes what curl does.

## Acceptance criteria

- [x] `Curl.Extension.GoogleCloudStorage.UnitLibrary` and `Curl.Extension.GoogleCloudStorage.UnitTests` exist in `/Extensions/GoogleCloudStorage/` in `Curl.slnx`, registered by one line in `ExtensionCatalogue.cs`, and the build's `VerifyExtensionIsolation` passes.
- [x] It provides `gs://bucket/object` URLs with OAuth bearer or HMAC keys.
- [x] Unit tests check its wire behaviour against named sections of Google Cloud Storage JSON and XML API, with no network (fake connections or recorded bytes).
- [x] With `CURL_EXTENSIONS=0` a URL with its scheme fails exactly as curl fails an unsupported scheme (exit 1, same stderr bytes); unit test.
- [x] An ADR marked "Decided by Claude under Stewart's delegation" records its scheme or option names, the specification version followed, and anything left out with why.
- [x] Its description appears in `curl --extensions` and `curl --ai-help extensions`.
- [x] The changed libraries meet the four quality gates (100% line and branch coverage, complexity at most 10, CRAP at most 30) by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

If this is larger than one `/task-run`, split it before starting (wire format, then the client flow, then the options). Never reference a `Curl.Protocol.*` library or another extension: code it shares with curl's protocols (framing, a handshake) moves into a shared layer under its own task first, or is written here.

- 2026-10-29 (lane 3): Split for size before any code, following BL-4124 (S3). All engine hooks exist: `ITransferContext.Credentials` (HMAC access id:secret from `--user`), `ITransferContext.Http.BearerToken` (`--oauth2-bearer`) and `ITransferContext.Upload` (`-T`). Filed BL-4284 (project scaffold and GOOG4-HMAC-SHA256 signer plus Bearer header), BL-4285 (XML API ListBucketResult and Error bodies) and BL-4286 (HTTP/1.1 exchange over IConnection). Planned default: use the XML API (storage.googleapis.com/bucket/object) for both Bearer and HMAC, single PUT for -T (GCS takes up to 5 TiB in one PUT, so no multipart), listing for an empty object or a trailing slash. This task keeps the handler, catalogue and description registration, ADR and the CURL_EXTENSIONS=0 test. Added ExtensionDescriptionCatalogue.cs to touches for the `--extensions` description (a union-merged list file, as BL-4124 did).
- 2026-10-29 (lane 9): Added `Client/` (GcsExtension, GcsProtocolHandler, GcsOperations, GcsClient, GcsEndpoint, GcsTransferException) on the S3 handler's shape, registered by one line each in ExtensionCatalogue.cs and ExtensionDescriptionCatalogue.cs; ADR-1095 records the choices (XML API for both bearer and HMAC, `--oauth2-bearer` wins over `--user`, one PUT per upload read into memory, `STORAGE_EMULATOR_HOST` for an emulator, `list-type=2` listing). Added `Curl.Console.UnitTests/CurlCompositionTests.cs` to touches: its pinned list of connecting handlers must name GcsProtocolHandler (no task in Doing on origin/work/dark-factory named it). The full build (with VerifyExtensionIsolation) is clean and the fast tests pass; Measure-CodeQuality.ps1 for GoogleCloudStorage and Composition reported 0 failing members. Tests are platform-neutral (no paths, OS errors or certificates), which covers CI on Linux and macOS.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 841a81a2 e5aed7cf e638f4e2 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Backlog. Split for size: waits on BL-4284 (project and GOOG4-HMAC-SHA256 signer), BL-4285 (XML bodies) and BL-4286 (HTTP/1.1 exchange)
- 2026-10-29: Backlog -> Doing. Criteria at claim: 841a81a2 e5aed7cf e638f4e2 bc772cb6 14cafed8 7ed051e6 e35aff77 3d836d5e.
- 2026-10-29: Doing -> Done. gs://bucket/object downloads, uploads and lists Google Cloud Storage objects with an OAuth bearer token or HMAC key
