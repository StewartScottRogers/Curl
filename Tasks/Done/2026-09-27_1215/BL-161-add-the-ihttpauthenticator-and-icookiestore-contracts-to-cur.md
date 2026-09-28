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
completed: 2026-09-26
---
# BL-161 — Add the IHttpAuthenticator and ICookieStore contracts to Curl.Protocol.Abstractions

## Goal

`IHttpAuthenticator`, `HttpAuthRequest`, `HttpAuthSchemes` and `ICookieStore` exist in `Curl.Protocol.Abstractions.UnitLibrary` as the BL-157 ADR states.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X5. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Implements the BL-157 ADR. Http consumes these; `Curl.Authentication` (BL-216 to BL-218) and `Curl.Cookies` (BL-220) implement them; `Curl.Console` composes (BL-237).

## Acceptance criteria

- [x] The contracts and `HttpAuthSchemes` flags match the BL-157 ADR member for member.
- [x] The existing reference-graph test (protocol libraries reference only Abstractions) passes; no new project reference is added.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions`.

## Notes

- Plan item: X5 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Plan: ADR-0014 is the plan, member for member, so the /feature architect, conformance and document stages were not run separately: the change adds contracts only, with no behaviour to plan or measure against curl, and each new type carries XML doc comments taken from the ADR.
- `HttpAuthSchemes` already existed (added by BL-159 in 5f4d607) and matches the ADR, so this task added `IHttpAuthenticator`, `HttpAuthRequest` and `ICookieStore`, plus `HttpAuthRequestTests` (constructor, value equality, `with` on every member) for coverage. The two interfaces have no code to cover.
- Verified: `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` clean; fast suite green (Abstractions 128 tests, `ProtocolIsolationTests` included); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary -SkipTestRun` reports 100% line, 100% branch, 259 members, 0 failing, worst CRAP 2.
- Seen, not this task: during the full measure run `Curl.Networking.UnitTests` `AuthenticateAsClientAsync_WithoutClientCertificate_PresentsNone` failed twice, though it passed in the fast run just before. Probably flaky under parallel lanes; nothing here touches Networking. Filed as BL-254.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. IHttpAuthenticator, HttpAuthRequest and ICookieStore exist in Curl.Protocol.Abstractions as ADR-0014 states, at 100% coverage
