---
id: BL-4350
title: Send WithRequest's body as the HTTP request body
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-4344]
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferContextRequests.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/RetargetedTransferContext.cs, Curl.Extension.Abstractions.UnitTests/Transfer/TransferContextRequestsTests.cs]
requirement: none
created: 2026-10-30
completed:
---
# BL-4350 — Send WithRequest's body as the HTTP request body

## Goal

A transfer hook that calls `context.WithRequest(url, method, body)` with a body has curl's HTTP handler send that body, so `--webdav-list` and `--webdav-sync` send their RFC 4918 `prop` request body on the wire.

## Context

Found in BL-4131 (ADR-1149) by running `curl -v --webdav-list` against a loopback PowerShell HttpListener: the `PROPFIND` went out with `Depth: 1` and `Content-Type`, but with no body. `TransferContextRequests.WithRequest` (BL-4303) puts the body in `RetargetedTransferContext.PostData`, while the HTTP handler sends `HttpRequestOptions.Body` (`Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs` builds it from `--data`), so the body is dropped for HTTP. The WebDAV listing still works because RFC 4918 section 9.1 treats an empty `PROPFIND` body as `allprop`. Depends on BL-4344 only because that task holds the whole `Curl.Extension.Abstractions.UnitTests` project.

## Acceptance criteria

- [ ] `WithRequest` with a non-null body gives a context whose `Http.Body` is a `BytesBody` of those bytes (content type `application/x-www-form-urlencoded`, as `--data-binary` sends; a `Content-Type` request header the hook adds still replaces it on the wire) and whose `PostData` is the same bytes; a null body keeps the transfer's own `Http.Body` and `PostData`.
- [ ] A unit test in `Curl.Extension.Abstractions.UnitTests/Transfer/TransferContextRequestsTests.cs` checks both cases.
- [ ] `Curl.Extension.Abstractions.UnitLibrary` meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-30: Created.
