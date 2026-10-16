---
id: BL-2432
title: Libcurl: CurlMulti get_handles, waitfds, get_offt, notify and CURLMOPT_* SetOption
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2429]
touches: [Libcurl/Multi/Settings, Libcurl.UnitTests/Multi/Settings, Libcurl/CLAUDE.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2432 — Libcurl: CurlMulti get_handles, waitfds, get_offt, notify and CURLMOPT_* SetOption

## Goal

Libcurl `CurlMulti` has `GetHandles`, `WaitFds`, `GetInfo(CurlMultiInfo)`, `NotifyEnable/Disable`, and `SetOption(CurlMultiOption, ...)` for every `CURLMOPT_*`.

## Context

ADR-0506; phase 3 of `Documentation/Planning/Curl-Api-And-libcurl.md`. Read the seam as it is in `Curl.Api.UnitLibrary` (EngineContracts, Options, Callbacks, Info, Codes) and the managed engine as it is in `Curl.Engine.UnitLibrary` now, not the plan's guesses, and correct the plan where they differ. Libcurl code and tests live in the area sub-folders named in `touches` (`Libcurl/<Area>` with its tests in `Libcurl.UnitTests/<Area>`) so areas run in parallel lanes; Libcurl tests use fakes of the seam interfaces, never the network. `--ai-help` is untouched (no option changes).

## Acceptance criteria

- [x] A test per `CURLMOPT_*` option and per `CURLMINFO_*`.
- [x] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged or grown, and every case it lists still passes.
- [x] `Measure-CodeQuality.ps1 -Library Libcurl` reports 0 failing members (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] Every new public member has an XML doc comment, and `Libcurl/CLAUDE.md` or `Curl.Engine.UnitLibrary`'s CLAUDE.md is kept true.

## Notes

- Built in `Libcurl/Multi/Settings` as a `CurlMulti` part: `SetOption(CurlMultiOption, object?)` over the
  internal `MultiOptionSetters` table, `GetHandles`, `WaitFds`, `GetInfo(CurlMultiInfo, out long)`,
  `NotifyEnable` and `NotifyDisable`. Decisions in ADR-0581 (ADR-0580 was already taken on the shared
  branch). The plan matched the seam; nothing to correct.
- `multi.c`'s range checks are taken from curl 8.22.0's source as read: no libcurl is installed on the
  lane machine to call `curl_multi_setopt` against.
- The function options take the seam's delegates (seeing `IEasyTransfer`), as the easy callback setters
  do; BL-2431 may wrap `SOCKETFUNCTION`/`TIMERFUNCTION` to hand the caller its `CurlEasy`.
- Added `Libcurl/CLAUDE.md` to `touches` to describe `Multi/Settings`; no task in Doing on
  `origin/work/dark-factory` names it.
- Tests: `MultiOptionSettersTests` (one per `CURLMOPT_*`) and `CurlMultiSettingsTests` (one row per
  `CURLMINFO_*`, plus `None` and unknown counts). `Measure-CodeQuality.ps1 -Library Libcurl`: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlMulti has SetOption for every CURLMOPT_*, GetHandles, WaitFds, GetInfo (curl_multi_get_offt) and NotifyEnable/Disable
