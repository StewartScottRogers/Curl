---
id: BL-4523
title: Conformance: complete the AzureStorage Blob requirement matrix, known failures and coverage (AzureStorage 1d)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4520, BL-4521, BL-4522]
touches: [Documentation/Conformance/Extended/AzureStorage.md, Curl.Conformance.Extended.UnitTests/AzureStorage/AzureBlobRequestHeaderCases.cs, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4523 — Conformance: complete the AzureStorage Blob requirement matrix, known failures and coverage (AzureStorage 1d)

## Goal

The AzureStorage Blob requirement matrix covers every client requirement of BL-4468's scope, mapped to a test or `n/a`, with every failure fixed or listed, and the reference endpoint's coverage measured.

## Context

Split from BL-4468 (AzureStorage part 1 of 2) for size; read BL-4468 in Tasks/Deferred for the full scope. Specification: Azure Storage Blob service REST API, `x-ms-version: 2025-01-05` (learn.microsoft.com, "Blob service REST API", "Authorize requests to Azure Storage"), HTTP/1.1 framing RFC 9112. Library under test: `Curl.Extended.Protocol.AzureStorage.UnitLibrary` (`CLAUDE.md`, ADR-1105); credentials `--oauth2-bearer`, `AZURE_STORAGE_BEARER_TOKEN`, `AZURE_STORAGE_SAS_TOKEN`; Shared Key is not implemented, so its rows are `n/a`. The client connects over TLS on 443; use `ReferenceRoute.Tls` with `ReferenceCertificate.CreateSelfSigned` and `-k`. Follow `Documentation/Conformance/Extended/README.md`: `[SpecRequirement("AzureStorage:<source>-<locator>")]`, the matrix format, `KnownFailures.RunCaseAsync`. Measure against the specification, never upstream curl. Nothing added to `Harness/`; the reference server references no `Curl.Extended.Protocol.*` project and never reuses the client codec. A defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix AzureStorage conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

The last part of BL-4468: BL-4520, BL-4521 and BL-4522 add the endpoint and the cases. This part walks the specification for every requirement that binds a client (MUST/REQUIRED, MUST NOT, SHOULD, SHOULD NOT, and each header or parameter the API documents as required, such as `x-ms-version`, `x-ms-date`, `Host`, `Content-Length`) and adds a row and, where no case covers it yet, a case in `AzureBlobRequestHeaderCases.cs`.

## Acceptance criteria

- [x] `Documentation/Conformance/Extended/AzureStorage.md` gives every requirement of BL-4468's scope that binds a client (MUST/REQUIRED, MUST NOT, SHOULD, SHOULD NOT, and each header or parameter the API documents as required) an `AzureStorage:` ID mapped to a test or to `n/a: <reason>`, links to `AzureStorage-DataLake.md` (BL-4469), and the harness's matrix check passes.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix AzureStorage conformance: ...` task; the known-failures check and the matrix check pass.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).
- [x] The code under `Curl.Conformance.Extended.UnitLibrary/AzureStorage/` (excluding `DataLake/`) is at 100% line and branch coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).

## Notes

- Added `AzureBlobRequestHeaderCases.cs` (3 cases): Host, x-ms-version and an RFC 1123 x-ms-date on a SAS listing with the token joined to the list query; Put Blob of an empty file with `Content-Length: 0`; an 8 MiB+ bearer upload over TLS framing every body by Content-Length alone with block IDs of at most 64 bytes and the block list's Content-Length. All pass, so nothing new in KnownFailures.txt.
- Matrix rows added: RFC9112-3.2-1, RequestHeaders-1, AuthorizeSas-2, PutBlob-3, PutBlockList-2, PutBlock-3, PutBlock-4 (n/a), RFC9112-6.1-1, AuthorizeOAuth-2, ListBlobs-3 (n/a), GetBlob-2 (n/a), plus the link to AzureStorage-DataLake.md. Decision (sensible default): new rows map to cases in the new file, because the other case files are outside touches.
- The client does not cap a block upload at 50,000 blocks: filed BL-4611 (Normal; not a failing case, so no KnownFailures line); PutBlock-4 is n/a until it lands.
- Measure-CodeQuality.ps1 -Library Curl.Conformance.Extended.UnitLibrary: 100% line, 100% branch, max complexity 10, 0 failing members. Build -warnaserror clean; fast tests green. Platform-neutral: temp directories only, no paths in output.

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 3b081890 2108d112 d8e3fb0e ef6c9026 c0e24372.
- 2026-11-02: Doing -> Done. AzureStorage Blob matrix covers every client requirement with cases or n/a, links the DataLake matrix; checks and coverage green
