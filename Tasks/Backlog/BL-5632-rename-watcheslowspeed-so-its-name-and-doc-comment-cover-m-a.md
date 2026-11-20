---
id: BL-5632
title: Rename WatchesLowSpeed so its name and doc comment cover -m as well as -Y and -y
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5632 — Rename WatchesLowSpeed so its name and doc comment cover -m as well as -Y and -y

## Goal

The engine's predicate that now covers `-m` as well as `-Y`/`-y` has a name and doc comment that say so.

## Context

Found by the shift-end review of 2026-11-20 (code-reviewer) in the diff b73256235592..071c3980c0.

`Curl.Engine.UnitLibrary/ManagedEasyTransfer.SetUp.cs:253-257`: `WatchesLowSpeed` now also returns true for `transfer.Timeout > TimeSpan.Zero` (`-m`, BL-5613). Its name, summary ("Tells whether `-Y` or `-y` is set") and returns text ("when the low-speed limit or time is above zero") still describe only the low-speed watch.

CLAUDE.md's "Say what it does, do what it says" counts this as a defect. A reader could take `-m` back out as "not low-speed" and bring back BL-5613, where `-m` loses the transfer sizes.

## Acceptance criteria

- [ ] The method is renamed to say it covers both watchdogs (for example `WatchdogMayEndTransfer`), and every caller is updated.
- [ ] Its summary and returns text name `-m` as well as `-Y` and `-y`.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-11-20: Created.
