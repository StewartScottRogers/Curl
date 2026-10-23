---
id: BL-3183
title: Blank option arguments are accepted or rejected as curl 8.22.0 does (BL-3171 item 1)
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineOptions.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs]
requirement: none
created: 2026-10-22
completed:
---
# BL-3183 — Blank option arguments are accepted or rejected as curl 8.22.0 does (BL-3171 item 1)

## Goal

Every option that takes an argument accepts or rejects an empty argument (`''`) exactly as curl 8.22.0 does, `--proxy-key ''` included.

## Context

BL-3171 item 1. Parsing is in `Curl.Cli.UnitLibrary/CommandLineOptions.cs` and `CommandLineOptionTable.cs`. Run every argument-taking option once with `''` through curl 8.22.0 and quote the accept/reject list in Notes.

Split from BL-3171 (one sub-criterion per subtask); read BL-3171's Context for the full item text. Measure curl 8.22.0 with Record-CurlExchange.ps1 before pinning (curl 8.22.0 Schannel is on PATH on the lane machines) and record the measurement in Notes. Build gate: `dotnet build <library> -warnaserror` clean, the matching .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for the changed library is inside every gate.

## Acceptance criteria

- [ ] A test in `Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs` runs every option that takes an argument with `''` and matches curl 8.22.0's measured accept/reject list (the list is quoted in Notes); `--proxy-key ''` has its own case.
- [ ] The changed library builds clean with -warnaserror, its .UnitTests fast tests pass, and Measure-CodeQuality.ps1 for it is inside every gate.

## Notes

## Log

- 2026-10-22: Created.
