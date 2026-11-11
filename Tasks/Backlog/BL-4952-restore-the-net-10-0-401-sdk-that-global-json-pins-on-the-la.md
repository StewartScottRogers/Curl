---
id: BL-4952
title: Restore the .NET 10.0.401 SDK that global.json pins on the lane host
priority: High
assignee: Stewart
pipeline: direct
depends-on: []
touches: [global.json]
requirement: none
created: 2026-11-11
completed:
---
# BL-4952 — Restore the .NET 10.0.401 SDK that global.json pins on the lane host

## Goal

Every lane host has the .NET SDK that `global.json` pins (10.0.401, rollForward latestFeature), so `dotnet build` runs without editing `global.json`.

## Context

On 2026-11-11 lane 1's host had only SDKs 10.0.100-rc.1.25451.107, 11.0.100-rc.2.26504.105 and 11.0.100 (installed 2026-11-10) under `C:\Program Files\dotnet\sdk`; every `dotnet` command failed with "A compatible .NET SDK was not found. Requested SDK version: 10.0.401". BL-4902 verified its change by pointing `global.json` at 11.0.100 for the run only and restoring it. Stewart: reinstall SDK 10.0.4xx on the host, or decide to move `global.json` to SDK 11 (a toolchain change).

## Acceptance criteria

- [ ] `dotnet --list-sdks` on the lane host lists an SDK that satisfies `global.json`, and `dotnet build` succeeds with `global.json` unchanged.

## Notes

## Log

- 2026-11-11: Created.
