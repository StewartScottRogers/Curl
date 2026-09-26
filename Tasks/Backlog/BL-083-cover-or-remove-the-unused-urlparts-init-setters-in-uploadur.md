---
id: BL-083
title: Cover or remove the unused UrlParts init setters in UploadUrl
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Cli.UnitLibrary, Curl.Cli.UnitTests]
requirement: none
created: 2026-09-26
completed:
---
# BL-083 — Cover or remove the unused UrlParts init setters in UploadUrl

## Goal

`Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports 100% line coverage, with no failing member.

## Context

- Found during BL-067 (2026-09-26): the audit reports `UrlParts.set_PathStart`,
  `set_QueryStart` and `set_FragmentStart` at 0% line coverage,
  `Curl.Cli.UnitLibrary/UploadUrl.cs:85`. They are the compiler-generated `init` setters of
  the positional `private readonly record struct UrlParts`, which nothing calls.
- Likely fix: make `UrlParts` a plain `readonly struct` (or a record struct with get-only
  properties set by a constructor), so no uncallable setter is emitted. No behaviour change.

## Acceptance criteria

- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Cli.UnitLibrary` reports line 100% and branch 100% with 0 failing members.
- [ ] `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` is green with the `UploadUrl` tests unchanged.

## Notes

## Log

- 2026-09-26: Created.
