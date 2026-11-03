---
id: BL-4566
title: Fix AzureStorage conformance: AzureStorage:DfsErrors-1 (read the DFS JSON error body)
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.AzureStorage.UnitLibrary/Client/AzureStorageClient.cs, Curl.Extended.Protocol.AzureStorage.UnitLibrary/Bodies, Curl.Extended.Protocol.AzureStorage.UnitTests, Curl.Conformance.Extended.UnitTests/KnownFailures.txt, Curl.Extended.Protocol.AzureStorage.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-11-02
completed: 2026-11-02
---
# BL-4566 — Fix AzureStorage conformance: AzureStorage:DfsErrors-1 (read the DFS JSON error body)

## Goal

A failed `abfs://` or `abfss://` operation whose DFS endpoint answers with the Data Lake JSON error body makes curl exit 22 with `Azure Storage returned error <status>: <code>: <message>`, as an `az://` failure with an XML `Error` body already does.

## Context

Found by BL-4469's case `AzureDataLakeTransferCases.Read_MissingPath_ReadsTheJsonErrorBodyAndExits22`
(`Curl.Conformance.Extended.UnitTests/AzureStorage/DataLake/`). The Azure Data Lake Storage Gen2 REST API answers every
failure with `{"error":{"code":"PathNotFound","message":"The specified path does not exist."}}` (Content-Type
`application/json`), but `AzureStorageClient.ErrorOf` reads the body only with `AzureStorageErrorParser`, which reads the
Blob service's XML `Error` element, so curl prints only `curl: (22) Azure Storage returned error 404`. Add a JSON reading
of the error body (BCL `System.Text.Json`, as `AzureStoragePathListParser` does), tried when the XML one finds no `Error`.

## Acceptance criteria

- [x] `AzureDataLakeTransferCases.Read_MissingPath_ReadsTheJsonErrorBodyAndExits22` passes with curl's stderr `curl: (22) Azure Storage returned error 404: PathNotFound: The specified path does not exist.`.
- [x] A focused unit test in `Curl.Extended.Protocol.AzureStorage.UnitTests` pins the JSON error body's code and message (ADR-0482).
- [x] The `AzureStorage:DfsErrors-1 BL-4563` line is removed from `Curl.Conformance.Extended.UnitTests/KnownFailures.txt`; the known-failures check passes.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests are green, and `Measure-CodeQuality.ps1 -Library Curl.Extended.Protocol.AzureStorage.UnitLibrary` reports every member inside the gates.

## Notes

- Added `Bodies/AzureStorageJsonErrorParser` (JsonDocument, `error.code` and `error.message`, a missing message reads empty as the XML parser does); `AzureStorageClient.ErrorOf` tries it when the XML `Error` reading finds nothing. The approach is the one the Context names, so no ADR.
- Focused tests: `AzureStorageJsonErrorParserTests` and `AzureStorageProtocolHandlerTests.ExecuteAsync_DfsErrorWithAJsonErrorBody_FailsWithExit22NamingTheCode`. The DataLake conformance case passes with the KnownFailures line removed (Curl.Conformance.Extended.UnitTests 475 passed, 0 skipped).
- Added the library's `CLAUDE.md` to touches to name the new parser there (say what it does); no other task in Doing names it.
- Build -warnaserror clean; fast tests green (115 test assemblies, 0 failed); Measure-CodeQuality for the library: 0 failing members. Platform-neutral (no paths, OS text or certificates).

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: a6e4583a 694a22a9 629c7dfb be38cb95.
- 2026-11-02: Doing -> Done. abfs/abfss failures with the DFS JSON error body exit 22 naming the code and message
