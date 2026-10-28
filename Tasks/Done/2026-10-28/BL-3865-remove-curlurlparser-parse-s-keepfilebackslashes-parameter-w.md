---
id: BL-3865
title: Remove CurlUrlParser.Parse's keepFileBackslashes parameter, which no longer changes anything
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3851, BL-3589]
touches: [Curl.Protocol.Abstractions.UnitLibrary/CurlUrlParser.cs, Curl.Protocol.Abstractions.UnitLibrary/CurlUrl.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-28
---
# BL-3865 — Remove CurlUrlParser.Parse's keepFileBackslashes parameter, which no longer changes anything

## Goal

`CurlUrlParser.Parse` has no `keepFileBackslashes` parameter, and `CurlUrl.TryParse` no longer passes one.

## Context

BL-3589 found that curl (8.21.0 Schannel and 8.18.0) keeps every backslash in a `file` URL, so it removed the conversion from `CurlUrlParser`; the parameter that turned the conversion off now does nothing (it is discarded with `_ = keepFileBackslashes;`). `CurlUrl.cs` was held by BL-3851, so BL-3589 left its caller alone. Remove the parameter, its doc paragraph and the discard, and the `keepFileBackslashes: true` argument in `CurlUrl.cs`.

## Acceptance criteria

- [x] `git grep keepFileBackslashes` finds nothing outside `Tasks/`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-26: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: e80e2c50 779790c0.
- 2026-10-28: Doing -> Done. CurlUrlParser.Parse no longer takes keepFileBackslashes; CurlUrl.TryParse and ADR-0919 updated
