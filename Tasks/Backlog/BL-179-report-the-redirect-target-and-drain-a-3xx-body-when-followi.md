---
id: BL-179
title: Report the redirect target and drain a 3xx body when following
priority: High
assignee: Claude
pipeline: protocol
depends-on: [BL-173]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-179 — Report the redirect target and drain a 3xx body when following

## Goal

For a 3xx the handler resolves `Location` against the request URL into `TransferReport.RedirectUrl`, and with `FollowRedirects` reads and discards the 3xx body while still writing its headers.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item H11. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-203 (redirect follower in Core) uses `RedirectUrl`; `-w %{redirect_url}` reads it without `-L` too.

## Acceptance criteria

- [ ] Relative, absolute and scheme-relative `Location` values resolve against the request URL into `Report.RedirectUrl`, with or without `FollowRedirects`.
- [ ] With `FollowRedirects`, the 3xx body is read and discarded (Content-Length and chunked), and its headers still go to `HeaderOutput`.
- [ ] `dotnet build Curl.Protocol.Http.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Http`. Tests use the fakes in `Curl.Protocol.Http.UnitTests/Fakes` (added by BL-169), never a socket; every parser test also runs with 1-byte chunks.

## Notes

- Plan item: H11 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
