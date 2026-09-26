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
completed:
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

- [ ] The types and members match the BL-157 ADR; `TransferContext.Http` defaults to null.
- [ ] `FakeTransferContext` is deleted and every `Curl.Protocol.File.UnitTests` test builds a `TransferContext` instead, with the same assertions passing.
- [ ] `dotnet build Curl.Protocol.Abstractions.UnitLibrary -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Protocol.Abstractions`.

## Notes

- Plan item: X3 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.

## Log

- 2026-09-26: Created.
