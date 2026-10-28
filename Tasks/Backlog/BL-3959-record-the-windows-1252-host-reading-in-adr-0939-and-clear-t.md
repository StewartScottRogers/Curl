---
id: BL-3959
title: Record the Windows-1252 host reading in ADR-0939 and clear the quality gates (CGR-059)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-3957, BL-3958]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-10-28
completed:
---
# BL-3959 — Record the Windows-1252 host reading in ADR-0939 and clear the quality gates (CGR-059)

## Goal

An ADR records that Curl reads a whole non-ASCII host as Windows-1252 on Windows, and `Curl.Protocol.Abstractions.UnitLibrary` is inside every quality gate.

## Context

Last part of BL-3953 (CGR-059). BL-3957 and BL-3958 change how `CurlUrlHost` reads a host. The earlier decision (BL-3884, which BL-3953 called ADR-0939) is found with `grep -ril "StrayBytesAsWindows1252" Documentation/Planning/Decisions`; ADR-0939 on disk is about a 101 response, so check the number. Amend that ADR or add a new one marked "Decided by Claude under Stewart's delegation", with the README row. Documentation only, plus whatever the quality run shows is missing.

## Acceptance criteria

- [ ] The ADR states the Windows-1252 reading, the UTF-8 locale answer on Linux and macOS, the `Location` host answer from BL-3958 and why, and the Decisions README lists it.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` shows no member of `Curl.Protocol.Abstractions.UnitLibrary` outside the gates (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3953, which went over its cost cap.
- Touches widened to the ADR folder, since the ADR lives in `Documentation/Planning/Decisions`.

## Log

- 2026-10-28: Created.
