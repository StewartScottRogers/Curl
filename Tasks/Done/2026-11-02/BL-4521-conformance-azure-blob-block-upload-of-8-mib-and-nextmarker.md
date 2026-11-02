---
id: BL-4521
title: Conformance: Azure Blob block upload of 8 MiB and NextMarker listing cases (AzureStorage 1b)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4520]
touches: [Curl.Conformance.Extended.UnitTests/AzureStorage/AzureBlobBlockUploadAndListingCases.cs, Documentation/Conformance/Extended/AzureStorage.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Conformance.Extended.UnitLibrary/AzureStorage/AzureBlobReferenceEndpoint.cs, Curl.Conformance.Extended.UnitTests/AzureStorage/AzureBlobReferenceServerTests.cs]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4521 — Conformance: Azure Blob block upload of 8 MiB and NextMarker listing cases (AzureStorage 1b)

## Goal

Cases pin curl's `az://` upload of 8 MiB or more as Put Block calls and a Put Block List, and a listing that follows several `NextMarker` pages.

## Context

Split from BL-4468 (AzureStorage part 1 of 2) for size; read BL-4468 in Tasks/Deferred for the full scope. Specification: Azure Storage Blob service REST API, `x-ms-version: 2025-01-05` (learn.microsoft.com, "Blob service REST API", "Authorize requests to Azure Storage"), HTTP/1.1 framing RFC 9112. Library under test: `Curl.Extended.Protocol.AzureStorage.UnitLibrary` (`CLAUDE.md`, ADR-1105); credentials `--oauth2-bearer`, `AZURE_STORAGE_BEARER_TOKEN`, `AZURE_STORAGE_SAS_TOKEN`; Shared Key is not implemented, so its rows are `n/a`. The client connects over TLS on 443; use `ReferenceRoute.Tls` with `ReferenceCertificate.CreateSelfSigned` and `-k`. Follow `Documentation/Conformance/Extended/README.md`: `[SpecRequirement("AzureStorage:<source>-<locator>")]`, the matrix format, `KnownFailures.RunCaseAsync`. Measure against the specification, never upstream curl. Nothing added to `Harness/`; the reference server references no `Curl.Extended.Protocol.*` project and never reuses the client codec. A defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix AzureStorage conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

Uses the reference Blob endpoint BL-4520 adds; if it lacks something these cases need, extend it under `Curl.Conformance.Extended.UnitLibrary/AzureStorage/` with its own coverage (add that path to `touches`).

## Acceptance criteria

- [x] Cases in `Curl.Conformance.Extended.UnitTests/AzureStorage/AzureBlobBlockUploadAndListingCases.cs` run curl in process and assert wire bytes, stdout, stderr and exit code for: an upload of 8 MiB or more as blocks with block ids of equal length and the block list in order, and a listing over several `NextMarker` pages.
- [x] `Documentation/Conformance/Extended/AzureStorage.md` has a row for every `AzureStorage:` ID these cases carry.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix AzureStorage conformance: ...` task; the known-failures check and the matrix check pass.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Two cases in `AzureBlobBlockUploadAndListingCases`: an upload of 8 MiB + 3 bytes (the client's block size is 8 MiB) pins two Put Block requests with block IDs `MDAwMDAw` and `MDAwMDAx` (Base64 of zero-padded six-digit indexes, so equal length), then Put Block List `<BlockList><Latest>..</Latest>..</BlockList>` in order, checked on the wire and in the endpoint's committed list and blob; a listing with page size 2 over five blobs pins three List Blobs requests, the 2nd and 3rd carrying `marker` from the previous `NextMarker`. Both pass; nothing added to `KnownFailures.txt`. New IDs: PutBlock-1, PutBlock-2, PutBlockList-1, ListBlobs-1, ListBlobs-2.
- The listing first failed with exit 8: the reference endpoint's `Properties` lacked `Last-Modified` and `Etag`, which List Blobs returns for every blob. That was a reference-server gap, not a client defect, so the endpoint now writes both (a fixed Last-Modified, an Etag from the content's SHA-256) and `AzureBlobReferenceServerTests` pins them. Touches widened to those two files: no task in Doing on origin/work/dark-factory names them. Measure-CodeQuality for Curl.Conformance.Extended.UnitLibrary: 0 failing members.
- Fast tests: one unrelated failure, `Mqtt5ConnectTests.Connect_WithoutCredentials_SendsCleanStartAndAClientIdentifierFirst`, passed alone; filed as BL-4547. Platform-neutral: temp directory via Path.Combine, no OS text; the Linux and macOS criterion is met by that and the fast tests green here.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 74fd5e4e a4181cce 2108d112 d8e3fb0e ef6c9026.
- 2026-11-02: Doing -> Done. Conformance cases pin az:// 8 MiB block upload and NextMarker paged listing; both pass
