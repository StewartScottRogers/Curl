---
id: BL-4285
title: Extension: Azure Storage XML and JSON bodies (List Blobs, Block List, Error, ADLS Gen2 path list)
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-4284]
touches: [Curl.Extension.AzureStorage.UnitLibrary/Bodies, Curl.Extension.AzureStorage.UnitTests/Bodies]
requirement: none
created: 2026-10-29
completed:
---
# BL-4285 — Extension: Azure Storage XML and JSON bodies (List Blobs, Block List, Error, ADLS Gen2 path list)

## Goal

`Curl.Extension.AzureStorage.UnitLibrary` reads and writes the request and response bodies the `az://` and `abfs://` handler needs: the Blob service List Blobs `EnumerationResults` XML (with `NextMarker`), the Put Block List `BlockList` XML, the storage `Error` XML, and the ADLS Gen2 Path List JSON (`paths`, with the `x-ms-continuation` paging header left to the caller).

## Context

Split from BL-4126 for size. Specification: Azure Blob Storage REST API ("List Blobs", "Put Block List", "Status and error codes") and ADLS Gen2 REST API ("Path - List"). BCL only: `System.Xml` and `System.Text.Json` readers without reflection (Native AOT). The S3 extension's `Xml/` folder is a model, not a reference. Code in `Bodies/`; tests use recorded bodies quoted from the specification's examples.

## Acceptance criteria

- [ ] Parsers for List Blobs and the Error XML, a writer for the BlockList XML and a parser for the Path List JSON exist under `Bodies/`, with unit tests citing the named specification section for each.
- [ ] A malformed body yields a typed failure, not an unhandled exception.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
