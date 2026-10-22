---
id: BL-1946
title: Bring Curl.Console back to its quality gates: CreateDialingSecurityContextFactory branch, RemoteHeaderNameStream.ReadLineAsync complexity 12
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1946 — Bring Curl.Console back to its quality gates: CreateDialingSecurityContextFactory branch, RemoteHeaderNameStream.ReadLineAsync complexity 12

## Goal

Measure-CodeQuality.ps1 -Library Curl.Console reports no failing member.

## Context

BL-1943's measurement (2026-10-09) found two members failing that BL-1943 did not change: `CurlComposition.CreateDialingSecurityContextFactory` (Curl.Console\CurlComposition.cs:280, branch coverage 50%) and `RemoteHeaderNameStream.ReadLineAsync` (RemoteHeaderNameStream.cs:119, cyclomatic complexity 12). Cover the missing branch and extract a private method from the complex one; never raise a threshold.

## Acceptance criteria

- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Console` lists 0 failing members for Curl.Console.
- [x] `dotnet build -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` is green.

## Notes

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Extracted ContentDispositionNameOf; pinned both CreateDialingSecurityContextFactory branches; 0 failing members for Curl.Console
