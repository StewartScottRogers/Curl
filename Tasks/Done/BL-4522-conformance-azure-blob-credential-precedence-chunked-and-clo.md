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
completed: 2026-11-02
---
# BL-4522 — Conformance: Azure Blob credential precedence, chunked and close-delimited response cases (AzureStorage 1c)

## Goal

Cases pin each `az://` credential source in order of precedence and curl's reading of chunked and close-delimited responses.

## Context

Split from BL-4468 (AzureStorage part 1 of 2) for size; read BL-4468 in Tasks/Deferred for the full scope. Specification: Azure Storage Blob service REST API, `x-ms-version: 2025-01-05` (learn.microsoft.com, "Blob service REST API", "Authorize requests to Azure Storage"), HTTP/1.1 framing RFC 9112. Library under test: `Curl.Extended.Protocol.AzureStorage.UnitLibrary` (`CLAUDE.md`, ADR-1105); credentials `--oauth2-bearer`, `AZURE_STORAGE_BEARER_TOKEN`, `AZURE_STORAGE_SAS_TOKEN`; Shared Key is not implemented, so its rows are `n/a`. The client connects over TLS on 443; use `ReferenceRoute.Tls` with `ReferenceCertificate.CreateSelfSigned` and `-k`. Follow `Documentation/Conformance/Extended/README.md`: `[SpecRequirement("AzureStorage:<source>-<locator>")]`, the matrix format, `KnownFailures.RunCaseAsync`. Measure against the specification, never upstream curl. Nothing added to `Harness/`; the reference server references no `Curl.Extended.Protocol.*` project and never reuses the client codec. A defect in the extension library is listed in `KnownFailures.txt` with a filed High task `Fix AzureStorage conformance: <requirement>` (pipeline feature; touches the library, its `.UnitTests` and `KnownFailures.txt`; criteria: the case passes, a focused unit test in the library `.UnitTests` pins the fix (ADR-0482), the `KnownFailures.txt` line is removed).

Uses the reference Blob endpoint BL-4520 adds; if it lacks something these cases need, extend it under `Curl.Conformance.Extended.UnitLibrary/AzureStorage/` with its own coverage (add that path to `touches`).

## Acceptance criteria

- [x] Cases in `Curl.Conformance.Extended.UnitTests/AzureStorage/AzureBlobCredentialAndFramingCases.cs` run curl in process and assert wire bytes, stdout, stderr and exit code for: each credential source (`--oauth2-bearer`, `AZURE_STORAGE_BEARER_TOKEN`, `AZURE_STORAGE_SAS_TOKEN`) in order of precedence, and chunked and close-delimited responses.
- [x] `Documentation/Conformance/Extended/AzureStorage.md` has a row for every `AzureStorage:` ID these cases carry.
- [x] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt` with the ID of its filed High `Fix AzureStorage conformance: ...` task; the known-failures check and the matrix check pass.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green; no test needs `TestCategory=Integration`.
- [x] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

- Six cases in `AzureBlobCredentialAndFramingCases`: `--oauth2-bearer` over both variables, `AZURE_STORAGE_BEARER_TOKEN` over `AZURE_STORAGE_SAS_TOKEN`, the SAS variable alone (with its leading `?`), an empty chunked body, a close-delimited body and a close-delimited `Error` body. The reference Blob endpoint needed nothing new.
- Decision: the service defines no credential order, so the `CredentialPrecedence-1..3` rows cite the extension's documented order (`AzureStorageCredential.Choose`, its description) at level MUST; `AuthorizeSas-1` cites "Authorize requests to Azure Storage". Too small for an ADR; recorded here.
- Failing, listed in `KnownFailures.txt` with filed High tasks: BL-4563 (the extension reads `Environment.GetEnvironmentVariable`, not `ProtocolHandlerServices.ReadEnvironmentVariable`, so the harness's environment never reaches it: `CredentialPrecedence-2`, `-3`, `AuthorizeSas-1`), BL-4564 (`ReferenceTcpDialer` never sends close_notify on a TLS route, so a close-delimited body fails with `MissingCloseNotifyException`: `RFC9112-6.3-1`, `-6.3-2`; BL-4540 in Doing separately makes that exception an exit code).
- Only test, matrix and task files changed, so no quality run was needed. Cases use no file paths or OS text, so they are platform-neutral; CI on Linux and macOS is left to the shift's watch. `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Conformance.Extended.UnitTests 323 passed).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: 604a567e a4181cce 2108d112 d8e3fb0e ef6c9026.
- 2026-11-02: Doing -> Done. Azure Blob credential precedence, chunked and close-delimited cases run; failures listed under BL-4563 and BL-4564
