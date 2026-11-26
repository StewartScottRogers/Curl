---
id: BL-5999
title: Order the using directives of Http2RequestHeaders.cs so dotnet format passes
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Original.Protocol.Http.UnitLibrary/Http2RequestHeaders.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5999 — Order the using directives of Http2RequestHeaders.cs so dotnet format passes

## Goal

`dotnet format --verify-no-changes` passes on `Curl.Original.Protocol.Http.UnitLibrary`.

## Context

Found while verifying BL-5998: `dotnet format Curl.Original.Protocol.Http.UnitLibrary/Curl.Original.Protocol.Http.UnitLibrary.csproj --verify-no-changes` reports `Http2RequestHeaders.cs(1,1): error IMPORTS: Fix imports ordering.` BL-5998 did not change that file.

## Acceptance criteria

- [ ] `dotnet format Curl.Original.Protocol.Http.UnitLibrary/Curl.Original.Protocol.Http.UnitLibrary.csproj --verify-no-changes` reports nothing.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 1a65582d 779790c0.
