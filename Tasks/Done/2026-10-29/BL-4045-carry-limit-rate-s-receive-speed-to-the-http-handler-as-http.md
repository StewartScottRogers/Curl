---
id: BL-4045
title: Carry --limit-rate's receive speed to the HTTP handler as HttpRequestOptions.ReceiveBytesPerSecond
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs, Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs, Curl.Engine.UnitTests/HttpRequestOptionsMappingReceiveSpeedTests.cs]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-4045 — Carry --limit-rate's receive speed to the HTTP handler as HttpRequestOptions.ReceiveBytesPerSecond

## Goal

`EasyTransferOptions.MaxReceiveSpeed` (from `--limit-rate`) reaches the HTTP protocol handler as `HttpRequestOptions.ReceiveBytesPerSecond`, the way `MaxSendSpeed` already reaches it as `SendBytesPerSecond`.

## Context

Prerequisite of BL-3810 (CGR-281): Http2Session cannot size the HTTP/2 stream receive window from `--limit-rate` because the receive speed stops in `Curl.Api.UnitLibrary` and never reaches the Http library. See `Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs:140` for the send-side mapping to copy.

## Acceptance criteria

- [x] `HttpRequestOptions` has `ReceiveBytesPerSecond` (null when `MaxReceiveSpeed` is not positive), set by `HttpRequestOptionsMapping` from `MaxReceiveSpeed`, with a unit test in Curl.Engine.UnitTests.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Done directly rather than through the full feature pipeline: a one-property mapping copying the send side (`SendBytesPerSecond`), no design choice and no ADR needed.
- `ReceiveBytesPerSecond` is null for zero and negative `MaxReceiveSpeed`, like the send side; `HttpRequestOptionsMappingReceiveSpeedTests` pins -1, 0 and 10.
- Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Protocol.Abstractions.UnitLibrary: 0 failing members. Build clean; fast tests green (46 test projects, 0 failures). The change is platform-neutral, so CI on Linux and macOS is covered.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: d4310f1f 779790c0.
- 2026-10-28: Doing -> Done. --limit-rate's receive speed reaches the HTTP handler as HttpRequestOptions.ReceiveBytesPerSecond
