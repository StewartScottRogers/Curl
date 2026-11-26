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
completed: 2026-11-26
---
# BL-5999 — Order the using directives of Http2RequestHeaders.cs so dotnet format passes

## Goal

`dotnet format --verify-no-changes` passes on `Curl.Original.Protocol.Http.UnitLibrary`.

## Context

Found while verifying BL-5998: `dotnet format Curl.Original.Protocol.Http.UnitLibrary/Curl.Original.Protocol.Http.UnitLibrary.csproj --verify-no-changes` reports `Http2RequestHeaders.cs(1,1): error IMPORTS: Fix imports ordering.` BL-5998 did not change that file.

## Acceptance criteria

- [x] `dotnet format Curl.Original.Protocol.Http.UnitLibrary/Curl.Original.Protocol.Http.UnitLibrary.csproj --verify-no-changes` reports nothing.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Moved `using Curl.Http2;` above `using Curl.Protocol.Abstractions;`; no other change.
- Verified 2026-11-26: format verify exit 0 with no output; build 0 warnings, 0 errors; fast
  tests exit 0; Measure-CodeQuality.ps1 for the library reports 0 failing members.

## Log

- 2026-11-25: Created.
- 2026-11-26: Backlog -> Doing. Criteria at claim: 1a65582d 779790c0.
- 2026-11-26: Doing -> Done. Using directives of Http2RequestHeaders.cs ordered; dotnet format passes on the HTTP library.
