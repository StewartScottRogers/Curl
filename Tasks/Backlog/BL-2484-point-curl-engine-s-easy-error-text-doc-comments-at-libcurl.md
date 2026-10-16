---
id: BL-2484
title: Point Curl.Engine's easy error text doc comments at Libcurl's CurlEasyErrorText
priority: Low
assignee: Claude
pipeline: docs
depends-on: [BL-2450]
touches: [Curl.Engine.UnitLibrary, Documentation/Planning/Curl-Api-And-libcurl.md]
requirement: none
created: 2026-10-16
completed:
---
# BL-2484 — Point Curl.Engine's easy error text doc comments at Libcurl's CurlEasyErrorText

## Goal

Every mention of `CurlEasyErrorText` in `Curl.Engine.UnitLibrary` (doc comments and `CLAUDE.md`) and in `Documentation/Planning/Curl-Api-And-libcurl.md` says it lives in `Libcurl/Misc/ErrorText` and is passed in by `Curl.Console`, not that it "stays in `Curl.Console`".

## Context

BL-2450 moved `CurlEasyErrorText` from `Curl.Console` to `Libcurl/Misc/ErrorText` (namespace `Libcurl.Misc.ErrorText`). BL-2440 held `Curl.Engine.UnitLibrary` at the time, so these stale mentions were left: `EngineComposition.cs` (three `easyErrorText` param comments), `ManagedCurlEngine.cs:46`, `ManagedEngineSharedState.cs:20`, `TransferPerformer.cs:17`, `Curl.Engine.UnitLibrary/CLAUDE.md` lines near 125 and 184, and the plan's lines near 80 and 206. Doc text only; no behaviour change. The engine must still not reference `Libcurl` (Libcurl references the engine).

## Acceptance criteria

- [ ] `grep -rn "CurlEasyErrorText" Curl.Engine.UnitLibrary Documentation/Planning/Curl-Api-And-libcurl.md` shows no line saying it is in or stays in `Curl.Console`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-16: Created.
