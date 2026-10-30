---
id: BL-3978
title: Compare Curl with real curl on the CGR-034 probe cases and meet the quality gates
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4072, BL-4073, BL-4069, BL-3977, BL-4075]
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
- 2026-10-29 run (lane 2): built the probe as a C# file-based app (`.cgr034probe/probe.cs`, not committed; its source, commands and results are in `Documentation/Reviews/Cgr034ProbeResults.md`). REF is Git for Windows' `C:Program FilesGitCrt64binibcurl-4.dll` (8.22.0, Schannel); the silent server for `C_75` is 21702 (sensible default, the report names none). PORT equals REF on `C_20`, `C_65`, `C_74` and on every request on the wire; `C_21` and `C_75` differ intermittently: the kick returns before every transfer waits on its real socket and tells `T0` (filed BL-4075, added to `depends-on`). `C_21`'s removal order varies in REF too. Measure-CodeQuality not run (no production code changed). Next claim: once BL-4075 is Done, rerun the probe, update the results table, then run the quality gate and the fast tests.

## Log

- 2026-10-28: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: ae632df0 d1c58933 b084e04d 779790c0.
- 2026-10-29: Doing -> Backlog. Needs BL-4075: PORT's socket-action kick intermittently returns before every transfer waits on its socket (C_21, C_75 differ from REF)
- 2026-10-29: Backlog -> Deferred. Split into BL-4084 (probe results) and BL-4085 (quality gates) after the cost cap
