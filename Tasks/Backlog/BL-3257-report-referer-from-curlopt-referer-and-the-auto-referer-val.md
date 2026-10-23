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
completed:
---
# BL-3257 — Report REFERER from CURLOPT_REFERER and the auto-referer value in Libcurl easy info

## Goal

`CURLINFO_REFERER` on a Libcurl easy handle returns the value set with `CURLOPT_REFERER`, and the auto-referer value after a redirect with `CURLOPT_AUTOREFERER`.

## Context

Part of BL-3193. `CURLINFO_REFERER` is null even after `CURLOPT_REFERER` is set. When the `TransferReport` carries no referer, the library handle needs `CURLOPT_REFERER` as a fallback where `Info` is built in `ManagedEasyTransfer.Finish.cs` (the ToolEvents path already falls back). Measure upstream libcurl 8.22.0 first. Tests use fakes or loopback only.

## Acceptance criteria

- [ ] A test shows REFERER returns the value set with CURLOPT_REFERER.
- [ ] A test shows REFERER returns the auto-referer value after a redirect with AUTOREFERER, matching upstream 8.22.0.
- [ ] Build clean with -warnaserror, fast tests pass, Measure-CodeQuality.ps1 0 failing members.

## Notes

- Split from BL-3193 (over its cost cap). No stash was left.

## Log

- 2026-10-23: Created.
