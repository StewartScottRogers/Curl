---
id: BL-4289
title: Extension: Azure Storage XML and JSON bodies (List Blobs, Block List, Error, ADLS Gen2 path list)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4288]
touches: [Curl.Extension.AzureStorage.UnitLibrary/Bodies, Curl.Extension.AzureStorage.UnitTests/Bodies, Curl.Extension.AzureStorage.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4289 — Extension: Azure Storage XML and JSON bodies (List Blobs, Block List, Error, ADLS Gen2 path list)

## Goal

`Curl.Extension.AzureStorage.UnitLibrary` reads and writes the request and response bodies the `az://` and `abfs://` handler needs: the Blob service List Blobs `EnumerationResults` XML (with `NextMarker`), the Put Block List `BlockList` XML, the storage `Error` XML, and the ADLS Gen2 Path List JSON (`paths`, with the `x-ms-continuation` paging header left to the caller).

## Context

Split from BL-4126 for size. Specification: Azure Blob Storage REST API ("List Blobs", "Put Block List", "Status and error codes") and ADLS Gen2 REST API ("Path - List"). BCL only: `System.Xml` and `System.Text.Json` readers without reflection (Native AOT). The S3 extension's `Xml/` folder is a model, not a reference. Code in `Bodies/`; tests use recorded bodies quoted from the specification's examples.

## Acceptance criteria

- [x] Parsers for List Blobs and the Error XML, a writer for the BlockList XML and a parser for the Path List JSON exist under `Bodies/`, with unit tests citing the named specification section for each.
- [x] A malformed body yields a typed failure, not an unhandled exception.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Added `Curl.Extension.AzureStorage.UnitLibrary/CLAUDE.md` to touches to describe `Bodies/`; no other task in Doing on origin/work/dark-factory names it.
- Defaults taken: the BlockList body is written without an XML declaration (as the S3 extension's writer does; the service accepts it), entries in the order given with each block's `Latest`/`Committed`/`Uncommitted` element. Path List reads `isDirectory` and `contentLength` as the strings the service sends and also as JSON booleans and numbers; an absent `isDirectory` is a file and an absent `contentLength` is 0. An empty `NextMarker` is the last page. Times are RFC 1123, as Azure sends them.
- `Measure-CodeQuality.ps1 -Library Curl.Extension.AzureStorage.UnitLibrary`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: 0b595677 a051d31c 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. Bodies parsers and writer landed; quality gates 100/100, build clean, fast tests green
