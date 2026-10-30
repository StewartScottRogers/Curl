---
id: BL-4306
title: ResumableUpload extension: project, HTTP/1.1 exchange over IConnection and resumable upload header fields
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.ResumableUpload.UnitLibrary, Curl.Extension.ResumableUpload.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-29
completed:
---
# BL-4306 — ResumableUpload extension: project, HTTP/1.1 exchange over IConnection and resumable upload header fields

## Goal

`Curl.Extension.ResumableUpload.UnitLibrary` exists with its wire format: one HTTP/1.1 request and response over an `IConnection`, and the header fields of the IETF resumable upload draft and tus 1.0, each formatted and parsed by its own type.

## Context

Split from BL-4129 (`--resumable-upload`, extension 28 of 63) for size: this is the wire format, BL-4307 the client flow, BL-4129 the option, registration, description and ADR. Extension rules: ADR-0999 and the lane rules' "Extensions". Never reference a `Curl.Protocol.*` library or another extension; model the HTTP/1.1 exchange on `Curl.Extension.AzureStorage.UnitLibrary/Http/` (request writer, response head reader, Content-Length and chunked bodies) and write it here. The hook will reach servers through `TransferHookServices.Connector` (ADR-1039), which opens TLS when `ConnectTarget.UseTls` asks. Use the `new-project` skill; add the `<Folder Name="/Extensions/ResumableUpload/">` line under the marker comment in `Curl.slnx`.

Specifications: draft-ietf-httpbis-resumable-upload (latest; header fields `Upload-Complete` and `Upload-Offset` as Structured Field boolean and integer, `Upload-Length`, `Upload-Limit`, `Upload-Draft-Interop-Version`, media type `application/partial-upload`, status 104) and tus 1.0 (`Tus-Resumable`, `Tus-Version`, `Tus-Extension`, `Upload-Offset`, `Upload-Length`, media type `application/offset+octet-stream`).

## Acceptance criteria

- [ ] `Curl.Extension.ResumableUpload.UnitLibrary` and `Curl.Extension.ResumableUpload.UnitTests` exist in `/Extensions/ResumableUpload/` in `Curl.slnx` and the build's `VerifyExtensionIsolation` passes.
- [ ] One HTTP/1.1 request (method, target, headers, optional body stream from an offset) is written to an `IConnection` and its response read: status, headers, and a Content-Length, chunked or empty body; unit tests with fake connections, no network.
- [ ] The IETF draft's header fields (`Upload-Complete` ?0/?1, `Upload-Offset`, `Upload-Length`, `Upload-Limit`) and tus 1.0's (`Tus-Resumable`, `Tus-Version`, `Tus-Extension`, `Upload-Offset`, `Upload-Length`) are formatted and parsed, refusing malformed values; unit tests name the draft and tus sections they check.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
