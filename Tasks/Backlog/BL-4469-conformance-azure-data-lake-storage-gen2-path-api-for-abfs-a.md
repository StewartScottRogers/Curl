---
id: BL-4469
title: Conformance: Azure Data Lake Storage Gen2 path API for abfs:// and abfss:// (AzureStorage part 2 of 2)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-4500, BL-4501, BL-4502, BL-4503, BL-4468]
touches: [Curl.Conformance.Extended.UnitLibrary/AzureStorage/DataLake, Curl.Conformance.Extended.UnitTests/AzureStorage/DataLake, Documentation/Conformance/Extended/AzureStorage-DataLake.md, Curl.Conformance.Extended.UnitTests/KnownFailures.txt]
requirement: none
created: 2026-11-02
completed:
---
# BL-4469 — Conformance: Azure Data Lake Storage Gen2 path API for abfs:// and abfss:// (AzureStorage part 2 of 2)

## Goal

Curl's `abfs://` and `abfss://` client is measured against the Azure Data Lake Storage Gen2 Path REST API through
the real curl command line, every client requirement it implements is in a matrix mapped to a test, and every
failure is fixed or listed with its fix task.

## Context

Specification: Azure Data Lake Storage Gen2 REST API, `x-ms-version: 2025-01-05` (learn.microsoft.com, "Azure Data
Lake Storage REST API"): Path - Read, Path - Create, Path - Update (`action=append` with `position`, then
`action=flush` with the final length), Path - List (JSON `paths`, `x-ms-continuation`), the error body, and the
`abfs[s]://filesystem@account.dfs.core.windows.net/path` URL form. Builds on the reference endpoint BL-4468 wrote in
`Curl.Conformance.Extended.UnitLibrary/AzureStorage/`; add the DFS endpoint in `AzureStorage/DataLake/` through its
extension point.

Library: `Curl.Extended.Protocol.AzureStorage.UnitLibrary` (`Client/AzureDataLakeOperations`,
`Bodies/AzureStoragePathListParser`, `Requests/AzureStorageUrlMapping`; ADR-1105).

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

- [ ] `Documentation/Conformance/Extended/AzureStorage-DataLake.md` gives every requirement of this scope that binds
      a client an `AzureStorage:` ID mapped to a test or to `n/a: <reason>`; the harness's matrix check passes.
- [ ] Cases in `Curl.Conformance.Extended.UnitTests/AzureStorage/DataLake/` run curl in process for `abfs://` and
      `abfss://` and assert wire bytes, stdout, stderr and exit code for at least: read, create then append then
      flush with the right `position` values, an empty upload, a listing over several `x-ms-continuation` pages, a
      bare account name and a full `dfs.core.windows.net` host, and an error body with exit 22.
- [ ] Every failing case is fixed in this task or listed in `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`
      with the ID of its filed High `Fix AzureStorage conformance: ...` task; the known-failures check passes.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is
      green; no test needs `TestCategory=Integration`.
- [ ] The code added under `Curl.Conformance.Extended.UnitLibrary/AzureStorage/DataLake/` is at 100% line and branch
      coverage, complexity at most 10 and CRAP at most 30 per method (`Measure-CodeQuality.ps1`).
- [ ] No package added; every test is platform-neutral (no drive letters, Windows-only paths or text).

## Notes

## Log

- 2026-11-02: Created.
