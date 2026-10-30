---
id: BL-4303
title: Extension abstractions: let a transfer hook send a request with another URL, method and body
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Abstractions.UnitLibrary/Transfer/TransferContextRequests.cs, Curl.Extension.Abstractions.UnitLibrary/Transfer/RetargetedTransferContext.cs, Curl.Extension.Abstractions.UnitTests/Transfer/TransferContextRequestsTests.cs, Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4303 — Extension abstractions: let a transfer hook send a request with another URL, method and body

## Goal

A transfer hook can run the next handler with a context whose URL, HTTP method and request body differ from the user's, so an extension (WebDAV `PROPFIND`, sync `GET`s) sends its own requests through the engine's HTTP stack (TLS, proxy, `--user`, redirects) rather than writing HTTP itself.

## Context

Found by BL-4131 (WebDAV list and sync): `TransferContextRequestHeaders.WithRequestHeaders` and `TransferContextOutputs.WithOutputs` exist, but `HookedTransferContext` forwards `Url` and `PostData` from the inner context, and nothing sets `HttpRequestOptions.CustomMethod` from a hook. Add `TransferContextRequests.WithRequest(this ITransferContext context, CurlUrl url, string? method, ReadOnlyMemory<byte>? body)` in `Curl.Extension.Abstractions.UnitLibrary/Transfer`, backed by its own `RetargetedTransferContext` (forwarding every other member to the inner context, as `HookedTransferContext` does), with `Http` set to `inner.Http with { CustomMethod = method }` and `PostData` set to the body. Read `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` first.

## Acceptance criteria

- [x] `TransferContextRequests.WithRequest` returns a context whose `Url`, `Http.CustomMethod` and `PostData` are the given ones and whose other members are the inner context's; unit tests.
- [x] A null method or body keeps the inner context's own; unit tests.
- [x] The library meets the four quality gates by `Measure-CodeQuality.ps1`.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

- Delivered directly rather than through the full /feature stages: the plan was fully given in Context (one extension method and one forwarding context), so the architect stage would only restate it.
- Added `Curl.Extension.Abstractions.UnitLibrary/CLAUDE.md` to touches to document `WithRequest` beside `WithOutputs`; no task in Doing on origin/work/dark-factory names it.
- Choice: for a transfer that is not HTTP (`Http` null) the method has nothing to set, so `Http` stays null and only the URL and body change; tested.
- Choice: `url` is required (null throws), like the other hooks' arguments.
- Measure-CodeQuality.ps1 -Library Curl.Extension.Abstractions.UnitLibrary: 0 failing members. Build clean, fast tests green (Curl.Extension.Abstractions.UnitTests 81 passed); the change is platform-neutral (no paths, OS text or certificates), so CI on Linux and macOS is met by that.

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: a694e611 f50b5c0a 3180db99 3d836d5e.
- 2026-10-29: Doing -> Done. A transfer hook's context.WithRequest(url, method, body) runs the next handler with another URL, HTTP method and request body
