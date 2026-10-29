---
id: BL-3978
title: Compare Curl with real curl on the CGR-034 probe cases and meet the quality gates
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4006, BL-4007, BL-3990, BL-3977]
touches: [Documentation/Reviews/Cgr034ProbeResults.md, Curl.Engine.UnitTests/Cgr034Tests.cs]
requirement: none
created: 2026-10-28
completed:
---
# BL-3978 — Compare Curl with real curl on the CGR-034 probe cases and meet the quality gates

## Goal

CGR-034's probe cases give the same return codes, callback sequences and bytes on the wire through Curl as through real curl, and `Curl.Engine.UnitLibrary` meets the quality gates.

## Context

Split from BL-3563 (CGR-034), the verification part. The probe is a C# file-based app (`dotnet run`, no Python): P/Invoke to a real `libcurl-4.dll` for REF and `Libcurl` for PORT, with a recording server on 127.0.0.1:21700 (`200 hello`) and 21701 (`200 other`), and 21799 closed. Cases `C_20`, `C_21`, `C_65`, `C_74`, `C_75`. Keep the probe under `Documentation/Reviews` or the temp area; do not commit it outside `touches`.

## Acceptance criteria

- [ ] The probe commands and both REF and PORT results for `C_20`, `C_21`, `C_65`, `C_74`, `C_75` are recorded in `Documentation/Reviews/Cgr034ProbeResults.md`, and PORT equals REF in each.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary` reports no member outside a gate for the files BL-3976 and BL-3977 changed.
- [ ] Repro variants that already matched curl still match.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.

## Notes

- Split from BL-3563 (cost cap).

## Log

- 2026-10-28: Created.
