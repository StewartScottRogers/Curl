---
id: BL-4520
title: Conformance: Azure Blob reference endpoint, download, small upload, Error body and anonymous cases (AzureStorage 1a)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/AzureStorage, Curl.Conformance.Extended.UnitTests/AzureStorage/AzureBlobTransferCases.cs, Curl.Conformance.Extended.UnitTests/AzureStorage/AzureBlobReferenceServerTests.cs, Documentation/Conformance/Extended/AzureStorage.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4520 — Conformance: Azure Blob reference endpoint, download, small upload, Error body and anonymous cases (AzureStorage 1a)

## Goal

A reference Azure Blob endpoint written from the specification serves curl's `az://` client in process, and cases pin download, a small upload, an `Error` body with exit 22 and anonymous access.

## Context

Split from BL-4468 (AzureStorage part 1 of 2) for size; read BL-4468 in Tasks/Deferred for the full scope. Specification: Azure Storage Blob service REST API, `x-ms-version: 2025-01-05` (learn.microsoft.com, "Blob service REST API", "Authorize requests to Azure Storage"), HTTP/1.1 framing RFC 9112. Library under test: `Curl.Extended.Protocol.AzureStorage.UnitLibrary` (`CLAUDE.md`, ADR-1105); credentials `--oauth2-bearer`, `AZURE_STORAGE_BEARER_TOKEN`, `AZURE_STORAGE_SAS_TOKEN`; Shared Key is not implemented, so its rows are `n/a`. The client connects over TLS on 443; use `ReferenceRoute.Tls` with `ReferenceCertificate.CreateSelfSigned` and `-k`. Follow `Documentation/Conformance/Extended/README.md`: `[SpecRequirement("AzureStorage:<source>-<locator>")]`, the matrix format, `KnownFailures.RunCaseAsync`. Measure against the specification, never upstream curl. Nothing added to `Harness/`; the reference server references no `Curl.Extended.Protocol.*` project and never reuses the client codec. A defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix AzureStorage conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

The server implements every Blob operation the scope needs, so BL-4521 and BL-4522 only add cases: Get Blob, Put Blob, Put Block, Put Block List (recording block ids and the list order), List Blobs with `EnumerationResults` and `NextMarker` paging, the `Error` XML body, a choice of Content-Length, chunked or close-delimited response framing, and a record of each request's headers (`x-ms-version`, `x-ms-date`, `Host`, `Content-Length`, `Authorization`, SAS query). Design it so BL-4469 adds the DFS endpoint in `AzureStorage/DataLake/` without editing these files (for example a shared HTTP/1.1 request reader and a per-endpoint operation table it can extend by new files).

## Acceptance criteria

- [x] A reference Blob endpoint in `Curl.Conformance.Extended.UnitLibrary/AzureStorage/` implements the operations above and lets BL-4469 add the DFS endpoint in `AzureStorage/DataLake/` without editing this task's files.
- [x] Cases in `Curl.Conformance.Extended.UnitTests/AzureStorage/AzureBlobTransferCases.cs` run curl in process and assert wire bytes, stdout, stderr and exit code for: download, a small upload, an `Error` body with exit 22, and anonymous access.
- [x] `Documentation/Conformance/Extended/AzureStorage.md` exists in the matrix format with a row for every `AzureStorage:` ID these cases carry.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix AzureStorage conformance: ...` task; the known-failures check and the matrix check pass.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).
- [x] The code added under `Curl.Conformance.Extended.UnitLibrary/AzureStorage/` is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`), with its own tests in `AzureBlobReferenceServerTests.cs` where the cases do not reach a branch.

## Notes

- Design: `AzureStorageHttpRequestReader` (shared HTTP/1.1 request reader), `AzureStorageHttpResponseWriter`
  (Content-Length, chunked or close framing), `AzureStorageHttpResponse.Error` (the `Error` body) and the abstract
  `AzureStorageReferenceEndpoint` (records every request, serves until close). `AzureBlobReferenceEndpoint` derives from it
  with an operation table (Get Blob, Put Blob, Put Block, Put Block List, List Blobs with `NextMarker` paging). BL-4469 adds
  `AzureStorage/DataLake/AzureDataLakeReferenceEndpoint.cs` deriving from the same base, editing none of these files.
- Choice: an anonymous request to a container without anonymous read access answers `404 ResourceNotFound`, as the Blob
  service documents for private containers; one to an unknown container `404 ContainerNotFound`; an unknown verb/comp
  `405 UnsupportedHttpVerb`.
- Choice: the curl cases pin `x-ms-date` with a test clock overriding only `GetUtcNow`; stderr's line end is
  `Environment.NewLine`, as curl writes it on each platform.
- Found: close-delimited framing over the TLS route makes the client throw `MissingCloseNotifyException` out of the run
  (filed BL-4524); the cases use chunked and Content-Length framing, and the Close framing is covered by
  `AzureBlobReferenceServerTests`. No case fails, so `KnownFailures.txt` is unchanged.
- Found: `AzureStorageExtension` reads `AZURE_STORAGE_*` from the process environment (`Environment.GetEnvironmentVariable`),
  not the run's, so a case cannot set them through the harness; these cases use `--oauth2-bearer` and anonymous access.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary`: 0 failing members. Build of
  `Curl.slnx -warnaserror` clean, fast tests green. CI on Linux and macOS: the tests are platform-neutral (in-memory
  streams, a temporary upload file by `Path.Combine`, no OS text).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 6ac4821f 960eb6cd 49bbfc2e 2108d112 d8e3fb0e ef6c9026 b73427b6.
- 2026-11-02: Doing -> Done. Reference Azure Blob endpoint and four az:// cases (download, small upload, Error exit 22, anonymous) pass; matrix and quality clean.
