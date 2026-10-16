---
id: BL-2467
title: Libcurl: cover CurlMulti.SetOption's last switch branch
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Libcurl/Multi/Settings, Libcurl.UnitTests/Multi/Settings]
requirement: none
created: 2026-10-16
completed: 2026-10-16
---
# BL-2467 — Libcurl: cover CurlMulti.SetOption's last switch branch

## Goal

`CurlMulti.SetOption` (`Libcurl/Multi/Settings/CurlMulti.Settings.cs:29`) is at 100% branch coverage, so `Libcurl` passes the quality gate.

## Context

BL-2446's `Measure-CodeQuality.ps1 -Library Libcurl,Curl.Engine.UnitLibrary` (2026-10-16) reported this member at 100% line and 87.5% branch coverage (complexity 8): one branch of its `(option, value) switch` is never taken by the fast tests - most likely a `SocketFunction` or `TimerFunction` option given a value that is not Libcurl's own callback type, which falls to `SetEngineOption`. BL-2446 did not change this file. Add the missing test in `Libcurl.UnitTests/Multi/Settings`.

## Acceptance criteria

- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

The missing branch was `TimerFunction` given a `CurlMultiSocketCallback`; added `SetOption_LibcurlsSocketCallbackForTimerFunction_AnswersBadFunctionArgument`. `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members.

## Log

- 2026-10-16: Created.
- 2026-10-16: Backlog -> Doing.
- 2026-10-16: Doing -> Done. Covered the last SetOption branch; Libcurl quality 0 failing
