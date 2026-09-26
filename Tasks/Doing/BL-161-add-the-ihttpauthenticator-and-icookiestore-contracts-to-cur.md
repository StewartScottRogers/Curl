---
id: BL-161
title: Add the IHttpAuthenticator and ICookieStore contracts to Curl.Protocol.Abstractions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-157]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-161 — Add the IHttpAuthenticator and ICookieStore contracts to Curl.Protocol.Abstractions

## Goal

`IHttpAuthenticator`, `HttpAuthRequest`, `HttpAuthSchemes` and `ICookieStore` exist in `Curl.Protocol.Abstractions.UnitLibrary` as the BL-157 ADR states.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Implements the BL-157 ADR. Http consumes these; `Curl.Authentication` (BL-216 to BL-218) and `Curl.Cookies` (BL-220) implement them; `Curl.Console` composes (BL-237).

## Acceptance criteria

- [ ] The contracts and `HttpAuthSchemes` flags match the BL-157 ADR member for member.
- [ ] The existing reference-graph test (protocol libraries reference only Abstractions) passes; no new project reference is added.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions`.

## Notes

- Plan item: X5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
