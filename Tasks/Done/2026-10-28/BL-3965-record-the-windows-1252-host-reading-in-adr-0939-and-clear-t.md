---
id: BL-3965
title: Record the Windows-1252 host reading in ADR-0939 and clear the quality gates (CGR-059)
priority: Normal
assignee: Claude
pipeline: docs
depends-on: [BL-3963, BL-3964]
touches: [Documentation/Planning/Decisions]
requirement: none
created: 2026-10-28
completed: 2026-10-28
---
# BL-3965 — Record the Windows-1252 host reading in ADR-0939 and clear the quality gates (CGR-059)

## Goal

An ADR records that Curl reads a whole non-ASCII host as Windows-1252 on Windows, and `Curl.Protocol.Abstractions.UnitLibrary` is inside every quality gate.

## Context

Last part of BL-3953 (CGR-059). BL-3963 and BL-3964 change how `CurlUrlHost` reads a host. The earlier decision (BL-3884, which BL-3953 called ADR-0939) is found with `grep -ril "StrayBytesAsWindows1252" Documentation/Planning/Decisions`; ADR-0939 on disk is about a 101 response, so check the number. Amend that ADR or add a new one marked "Decided by Claude under Stewart's delegation", with the README row. Documentation only, plus whatever the quality run shows is missing.

## Acceptance criteria

- [x] The ADR states the Windows-1252 reading, the UTF-8 locale answer on Linux and macOS, the `Location` host answer from BL-3964 and why, and the Decisions README lists it.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` shows no member of `Curl.Protocol.Abstractions.UnitLibrary` outside the gates (100% line and branch coverage, complexity at most 10, CRAP at most 30).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3953, which went over its cost cap.
- Touches widened to the ADR folder, since the ADR lives in `Documentation/Planning/Decisions`.
- Amended ADR-0953 (README row updated) with the whole-host reading per platform. Quality run found `CurlUrlHost.EncodeLabel` at complexity 12; extracted `TryDecodeConvertibleRune`, rerun shows 0 failing members, 100% line and branch.

## Log

- 2026-10-28: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 2a4d9734 c155faee 779790c0.
- 2026-10-28: Doing -> Done. ADR-0953 records the Windows-1252 and UTF-8 locale readings; Abstractions library inside every gate
