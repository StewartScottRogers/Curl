---
id: BL-4522
title: Conformance: Azure Blob credential precedence, chunked and close-delimited response cases (AzureStorage 1c)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4505, BL-4506, BL-4507, BL-4502, BL-4503, BL-4520]
touches: [Curl.Conformance.Extended.UnitTests/AzureStorage/AzureBlobCredentialAndFramingCases.cs, Documentation/Conformance/Extended/AzureStorage.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4522 — Conformance: Azure Blob credential precedence, chunked and close-delimited response cases (AzureStorage 1c)

## Goal

Cases pin each `az://` credential source in order of precedence and curl's reading of chunked and close-delimited responses.

## Context

Split from BL-4468 (AzureStorage part 1 of 2) for size; read BL-4468 in Tasks/Deferred for the full scope. Specification: Azure Storage Blob service REST API, `x-ms-version: 2025-01-05` (learn.microsoft.com, "Blob service REST API", "Authorize requests to Azure Storage"), HTTP/1.1 framing RFC 9112. Library under test: `Curl.Extended.Protocol.AzureStorage.UnitLibrary` (`CLAUDE.md`, ADR-1105); credentials `--oauth2-bearer`, `AZURE_STORAGE_BEARER_TOKEN`, `AZURE_STORAGE_SAS_TOKEN`; Shared Key is not implemented, so its rows are `n/a`. The client connects over TLS on 443; use `ReferenceRoute.Tls` with `ReferenceCertificate.CreateSelfSigned` and `-k`. Follow `Documentation/Conformance/Extended/README.md`: `[SpecRequirement("AzureStorage:<source>-<locator>")]`, the matrix format, `KnownFailures.RunCaseAsync`. Measure against the specification, never upstream curl. Nothing added to `Harness/`; the reference server references no `Curl.Extended.Protocol.*` project and never reuses the client codec. A defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix AzureStorage conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

Uses the reference Blob endpoint BL-4520 adds; if it lacks something these cases need, extend it under `Curl.Conformance.Extended.UnitLibrary/AzureStorage/` with its own coverage (add that path to `touches`).

## Acceptance criteria

- [ ] Cases in `Curl.Conformance.Extended.UnitTests/AzureStorage/AzureBlobCredentialAndFramingCases.cs` run curl in process and assert wire bytes, stdout, stderr and exit code for: each credential source (`--oauth2-bearer`, `AZURE_STORAGE_BEARER_TOKEN`, `AZURE_STORAGE_SAS_TOKEN`) in order of precedence, and chunked and close-delimited responses.
- [ ] `Documentation/Conformance/Extended/AzureStorage.md` has a row for every `AzureStorage:` ID these cases carry.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix AzureStorage conformance: ...` task; the known-failures check and the matrix check pass.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 604a567e a4181cce 2108d112 d8e3fb0e ef6c9026.
