---
id: BL-2728
title: Add KeepSendingOnError to HttpRequestOptions for CURLOPT_KEEP_SENDING_ON_ERROR
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2728 — Add KeepSendingOnError to HttpRequestOptions for CURLOPT_KEEP_SENDING_ON_ERROR

## Goal

`HttpRequestOptions` carries `KeepSendingOnError` (default `false`), libcurl's `CURLOPT_KEEP_SENDING_ON_ERROR`, so the engine can hand it to the HTTP handler.

## Context

Found by BL-2646 (upstream case 1533). `Curl.Api.UnitLibrary/Options/Behaviour/EasyBehaviourOptions.cs` already has `KeepSendingOnError` and Libcurl sets it, but `Curl.Protocol.Abstractions.UnitLibrary/HttpRequestOptions.cs` has no field for it, so nothing reaches the HTTP handler. BL-2729 uses the field in the handler; BL-2646 maps it in `Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs`.

## Acceptance criteria

- [x] `HttpRequestOptions.KeepSendingOnError` exists, is `false` by default, and its XML doc comment names `CURLOPT_KEEP_SENDING_ON_ERROR`.
- [x] A test in `Curl.Protocol.Abstractions.UnitTests` pins its default.
- [x] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: a282a041 f434e32c 3d836d5e.
- 2026-10-18: Doing -> Done. KeepSendingOnError added with default-pinning test
