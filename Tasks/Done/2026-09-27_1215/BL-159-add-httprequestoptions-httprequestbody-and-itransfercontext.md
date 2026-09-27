---
id: BL-159
title: Add HttpRequestOptions, HttpRequestBody and ITransferContext.Http to Curl.Protocol.Abstractions
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-157]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Protocol.File.UnitTests]
requirement: none
created: 2026-09-26
completed: 2026-09-26
---
# BL-159 — Add HttpRequestOptions, HttpRequestBody and ITransferContext.Http to Curl.Protocol.Abstractions

## Goal

`ITransferContext` exposes `HttpRequestOptions? Http`, and `HttpRequestOptions`, `HttpRequestBody`, `BytesBody` and `StreamBody` exist exactly as the BL-157 ADR states.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item X3. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- Implements the BL-157 ADR (read it first).
- Adding a member to `ITransferContext` breaks `Curl.Protocol.File.UnitTests/Fakes/FakeTransferContext.cs`, the only other implementer (`grep ': ITransferContext'`). ADR-0006 expects fakes to be replaced by `TransferContext`; do that here.
- BL-134 (progress sink) also touches these projects; whichever lands second rebases on the first.

## Acceptance criteria

- [x] The types and members match the BL-157 ADR; `TransferContext.Http` defaults to null.
- [x] `FakeTransferContext` is deleted and every `Curl.Protocol.File.UnitTests` test builds a `TransferContext` instead, with the same assertions passing.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions`.

## Notes

- Plan item: X3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Decision (unattended run): ADR-0014's `HttpRequestOptions` has members of type `HttpAuthSchemes` (assigned to BL-161) and `ProxyEndpoint` (BL-162), and neither existed, so `HttpRequestOptions` could not be built "exactly as the ADR states" without them. They live in `Curl.Protocol.Abstractions.UnitLibrary`, inside this task's `touches`, so this task added `HttpAuthSchemes`, `ProxyKind` and `ProxyEndpoint` (validated like `ConnectTarget`) as well as `HttpFailMode` and `HttpVersionPreference`. BL-161 still owns `IHttpAuthenticator`, `HttpAuthRequest` and `ICookieStore`; BL-162 still owns `ConnectTarget.Proxy`, and finds `ProxyEndpoint` already present with its validation tests (`ProxyEndpointTests`).
- `HttpRequestBody` takes `ContentType` through a protected constructor that throws `ArgumentNullException` (param name `ContentType`) on null; the empty string is accepted, since the ADR only forbids null. The derived positional records inherit the read-only `ContentType`, so `with { ContentType = ... }` does not compile - the ADR's "exposes it read-only".
- `StreamBody.Length` is not range-checked; the ADR states no rule for a negative length.
- `FakeTransferContext` and `FakeTimeProvider` (used only by it) were deleted; 131 call sites now build `TransferContext`, filling `Output` with a `ChunkRecordingStream` where the fake defaulted it, and `Url = FileUrl` where the helper used to set it. `FileProtocolHandler` never reads `TimeProvider`, so `TimeProvider.System` changes no assertion.
- Results: build clean with `-warnaserror`; fast tests all green (Abstractions and File 263 among them); `Measure-CodeQuality.ps1 -Library Curl.Protocol.Abstractions.UnitLibrary` reports 100% line, 100% branch, 172 members, 0 failing, worst CRAP 1.

## Log

- 2026-09-26: Created.
- 2026-09-26: Backlog -> Doing.
- 2026-09-26: Doing -> Done. ITransferContext.Http, HttpRequestOptions, HttpRequestBody, BytesBody and StreamBody exist per ADR-0014; File tests use TransferContext
