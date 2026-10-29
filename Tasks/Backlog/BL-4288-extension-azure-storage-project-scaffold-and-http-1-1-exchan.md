---
id: BL-4288
title: Extension: Azure Storage project scaffold and HTTP/1.1 exchange over IConnection
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.AzureStorage.UnitLibrary, Curl.Extension.AzureStorage.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4288 — Extension: Azure Storage project scaffold and HTTP/1.1 exchange over IConnection

## Goal

`Curl.Extension.AzureStorage.UnitLibrary` and its `.UnitTests` project exist in `/Extensions/AzureStorage/` in `Curl.slnx`, and the library sends one HTTP/1.1 request over an `IConnection` and reads the response's status, headers and body (Content-Length, chunked, or until close), so the `az://` and `abfs://` handler can talk to Azure Storage without referencing `Curl.Protocol.Http`.

## Context

Split from BL-4126 for size, following the S3 split (BL-4197, BL-4199). Create both projects with the `new-project` skill as an extension (ADR-0999, ADR-1003: one single-line `<Folder>` under the marker comment in `Curl.slnx`). An extension never references a `Curl.Protocol.*` library or another extension, so the HTTP/1.1 client is written here; `Curl.Extension.S3.UnitLibrary/Http/` (ADR-1018) is the model to follow, rewritten under this library's names, not referenced. RFC 9112 §3, §4, §5, §6.3, §7.1. Request body streamed from a `Stream` of known length; response body exposed as a stream; keep-alive. Code in `Http/`; tests use a fake `IConnection` with recorded bytes. Do not register it in `ExtensionCatalogue.cs` (BL-4126 does).

## Acceptance criteria

- [ ] `Curl.Extension.AzureStorage.UnitLibrary` and `Curl.Extension.AzureStorage.UnitTests` exist in `/Extensions/AzureStorage/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [ ] The exchange under `Http/` writes the request bytes and reads Content-Length, chunked and close-delimited responses, with unit tests over a fake connection citing the RFC 9112 section for each.
- [ ] A malformed status line or a connection closed early yields a typed failure, not an unhandled exception.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
