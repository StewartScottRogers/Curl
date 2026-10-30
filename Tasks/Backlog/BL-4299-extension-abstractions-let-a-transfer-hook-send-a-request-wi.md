---
id: BL-4299
title: Extension abstractions: let a transfer hook send a request with another URL, method and body
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferContextRequests.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/RetargetedTransferContext.cs, Curl.Extension.Abstractions.UnitTests/Transfer/TransferContextRequestsTests.cs]
requirement: none
created: 2026-10-29
completed:
---
# BL-4299 — Extension abstractions: let a transfer hook send a request with another URL, method and body

## Goal

A transfer hook can run the next handler with a context whose URL, HTTP method and request body differ from the user's, so an extension (WebDAV `PROPFIND`, sync `GET`s) sends its own requests through the engine's HTTP stack (TLS, proxy, `--user`, redirects) rather than writing HTTP itself.

## Context

Found by BL-4131 (WebDAV list and sync): `TransferContextRequestHeaders.WithRequestHeaders` and `TransferContextOutputs.WithOutputs` exist, but `HookedTransferContext` forwards `Url` and `PostData` from the inner context, and nothing sets `HttpRequestOptions.CustomMethod` from a hook. Add `TransferContextRequests.WithRequest(this ITransferContext context, CurlUrl url, string? method, ReadOnlyMemory<byte>? body)` in `Curl.Extension.Abstractions.UnitLibrary/Transfer`, backed by its own `RetargetedTransferContext` (forwarding every other member to the inner context, as `HookedTransferContext` does), with `Http` set to `inner.Http with { CustomMethod = method }` and `PostData` set to the body. Read `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` first.

## Acceptance criteria

- [ ] `TransferContextRequests.WithRequest` returns a context whose `Url`, `Http.CustomMethod` and `PostData` are the given ones and whose other members are the inner context's; unit tests.
- [ ] A null method or body keeps the inner context's own; unit tests.
- [ ] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-29: Created.
