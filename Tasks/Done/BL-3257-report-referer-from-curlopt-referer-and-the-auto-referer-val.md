---
id: BL-3257
title: Report REFERER from CURLOPT_REFERER and the auto-referer value in Libcurl easy info
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs, Libcurl.UnitTests/Info/EasyInfoAuthAndRefererTests.cs]
requirement: none
created: 2026-10-23
completed: 2026-10-23
---
# BL-3257 — Report REFERER from CURLOPT_REFERER and the auto-referer value in Libcurl easy info

## Goal

`CURLINFO_REFERER` on a Libcurl easy handle returns the value set with `CURLOPT_REFERER`, and the auto-referer value after a redirect with `CURLOPT_AUTOREFERER`.

## Context

Part of BL-3193. `CURLINFO_REFERER` is null even after `CURLOPT_REFERER` is set. When the `TransferReport` carries no referer, the library handle needs `CURLOPT_REFERER` as a fallback where `Info` is built in `ManagedEasyTransfer.Finish.cs` (the ToolEvents path already falls back). Measure upstream libcurl 8.22.0 first. Tests use fakes or loopback only.

## Acceptance criteria

- [x] A test shows REFERER returns the value set with CURLOPT_REFERER.
- [x] A test shows REFERER returns the auto-referer value after a redirect with AUTOREFERER, matching upstream 8.22.0.
- [x] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- Split from BL-3193 (over its cost cap). No stash was left.
- `PerformedInfoOf` now gives every perform (library handle and tool alike) `Referer = info.Referer ?? options.Http.Referer`; the line moved out of `WithToolAnswers`. With `-L`, `RedirectFollower` already put the last hop's sent referer (the auto value under AUTOREFERER) on the report; without it the report has none, so CURLOPT_REFERER is the fallback.
- Upstream 8.22.0: CURLINFO_REFERER answers `data->state.referer`, set from CURLOPT_REFERER and replaced by `Curl_follow` under AUTOREFERER with the previous URL minus user info and fragment; the curl tool's `%{referer}` reads the same info and was measured in BL-361 (ADR-0101). No new libcurl measurement was taken: the tool measurement covers the same info and no C libtest harness exists in this lane.
- Tests: `Libcurl.UnitTests/Info/EasyInfoAuthAndRefererTests.cs` over the real HTTP handler and a scripted connection. Build clean with -warnaserror, fast tests green, Measure-CodeQuality -Library Curl.Engine.UnitLibrary 0 failing members. Platform-neutral (no paths, OS text or certificates).

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: e0ce4d5d 0abf45e2 3639a5e6.
- 2026-10-23: Doing -> Done. CURLINFO_REFERER answers CURLOPT_REFERER, and the auto-referer value after a followed redirect, on a Libcurl handle
