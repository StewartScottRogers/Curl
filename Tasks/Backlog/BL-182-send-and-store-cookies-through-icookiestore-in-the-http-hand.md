---
id: BL-182
title: Send and store cookies through ICookieStore in the HTTP handler
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: [BL-173, BL-161]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-182 — Send and store cookies through ICookieStore in the HTTP handler

## Goal

When an `ICookieStore` is supplied the handler sends its Cookie header and hands every Set-Cookie, including those on 3xx responses, back to the store.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H14. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- `ICookieStore.GetCookieHeader(Uri, bool secure, DateTimeOffset)` and `StoreFromResponse(Uri, IReadOnlyList<string>, DateTimeOffset)` (BL-161). Time comes from `ITransferContext.TimeProvider`.

## Acceptance criteria

- [ ] The Cookie header from the store is sent in curl's position (measured); no header when the store returns null.
- [ ] Every Set-Cookie value, including on a 3xx, is passed to `StoreFromResponse` in received order.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H14 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
