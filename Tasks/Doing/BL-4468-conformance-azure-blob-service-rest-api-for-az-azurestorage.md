---
id: BL-4468
title: Conformance: Azure Blob service REST API for az:// (AzureStorage part 1 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503]
touches: [Curl.Conformance.Extended.UnitLibrary/AzureStorage, Curl.Conformance.Extended.UnitTests/AzureStorage, Documentation/Conformance/Extended/AzureStorage.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4468 — Conformance: Azure Blob service REST API for az:// (AzureStorage part 1 of 2)

## Goal

Curl's `az://` client is measured against the Azure Blob service REST API (version 2025-01-05) and its request
authorization through the real curl command line, every client requirement it implements is in a matrix mapped to
a test, and every failure is fixed or listed with its fix task.

## Context

Specification: Azure Storage Blob service REST API, `x-ms-version: 2025-01-05` (learn.microsoft.com, "Blob service
REST API"): Get Blob, Put Blob, Put Block and Put Block List (from 8 MiB), List Blobs (`EnumerationResults`,
`NextMarker`), the `Error` response body, the request headers every operation requires (`x-ms-version`, `x-ms-date`,
`Host`, `Content-Length`), SAS token and OAuth bearer authorization ("Authorize requests to Azure Storage"), and the
HTTP/1.1 framing (RFC 9112) the library's own client speaks. Shared Key is not implemented (library `CLAUDE.md`), so
its rows are `n/a`. Part 2 (BL-4469) covers Data Lake Storage Gen2 (`abfs://`, `abfss://`).

Library: `Curl.Extended.Protocol.AzureStorage.UnitLibrary` (`CLAUDE.md`: `Client/`, `Http/`, `Bodies/`,
`Requests/`; ADR-1105). Credentials: `--oauth2-bearer`, `AZURE_STORAGE_BEARER_TOKEN`, `AZURE_STORAGE_SAS_TOKEN`.
The client connects over TLS on 443; the harness's TLS route stands in for the endpoint.

Part of the extended-protocol conformance plan Stewart approved on 2026-11-02. Follow
`Documentation/Conformance/Extended/README.md` and the ADR BL-4462 records: the harness, `[SpecRequirement]` IDs
(`AzureStorage:<source>-<locator>`), the matrix format and the `KnownFailures.txt` rule. Measure against the
specification, never against upstream curl. The reference server implements the API from the specification itself
and references no `Curl.Extended.Protocol.*` project. One file per case group; nothing added to `Harness/`. This task
touches only its own conformance files so protocols run in parallel: a fault in its own server or cases is fixed
here, but a defect in the extension library is listed in `KnownFailures.txt` with a filed High task
`Fix AzureStorage conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and
`KnownFailures.txt`; criteria: the case passes, a focused unit test in the library's `.UnitTests` pins the fix
(ADR-0482), the `KnownFailures.txt` line is removed).

## Acceptance criteria

- [ ] `Documentation/Conformance/Extended/AzureStorage.md` gives every requirement of this scope that binds a client
      (MUST/REQUIRED, MUST NOT, SHOULD, SHOULD NOT, and each header or parameter the API documents as required) an
      `AzureStorage:` ID mapped to a test or to `n/a: <reason>`, links to `AzureStorage-DataLake.md` (BL-4469), and
      the harness's matrix check passes.
- [ ] A reference Blob endpoint in `Curl.Conformance.Extended.UnitLibrary/AzureStorage/` lets BL-4469 add the DFS
      endpoint in `AzureStorage/DataLake/` without editing this task's files.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/AzureStorage/` run curl in process and assert wire bytes, stdout,
      stderr and exit code for at least: download, a small upload, an upload of 8 MiB or more as blocks with block ids
      of equal length and the block list in order, a listing over several `NextMarker` pages, an `Error` body with exit
      22, each credential source in order of precedence, anonymous access, chunked and close-delimited responses.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix AzureStorage conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/AzureStorage/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 69e23036 24c40727 82a03bfc 9592b2e5 d8e3fb0e a8cb0c29 ef6c9026.
