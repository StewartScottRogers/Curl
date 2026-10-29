---
id: BL-4084
title: Rerun the CGR-034 probe against real curl and record REF and PORT results
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-3846, BL-3847, BL-3859, BL-3890, BL-4015, BL-4016, BL-4007, BL-4072, BL-4073, BL-4069, BL-3977, BL-4075, BL-4086]
touches: [Documentation/Reviews/Cgr034ProbeResults.md]
requirement: none
created: 2026-10-29
completed: 2026-10-29
---
# BL-4084 — Rerun the CGR-034 probe against real curl and record REF and PORT results

## Goal

CGR-034's probe cases `C_20`, `C_21`, `C_65`, `C_74`, `C_75` give the same return codes, callback sequences and bytes on the wire through Curl (PORT) as through real curl (REF), and the results are recorded.

## Context

Split from BL-3978 (cost cap). The probe is a C# file-based app (`dotnet run`, no Python): P/Invoke to a real `libcurl-4.dll` for REF and `Libcurl` for PORT, with a recording server on 127.0.0.1:21700 (`200 hello`) and 21701 (`200 other`), 21702 silent (for `C_75`), and 21799 closed. A first version is in `Documentation/Reviews/Cgr034ProbeResults.md`; BL-4075 (now Done) fixed the socket-action kick that made `C_21` and `C_75` differ. Keep the probe under `Documentation/Reviews` or the temp area; do not commit it outside `touches`.

## Acceptance criteria

- [x] The probe commands and both REF and PORT results for `C_20`, `C_21`, `C_65`, `C_74`, `C_75` are recorded in `Documentation/Reviews/Cgr034ProbeResults.md`, and PORT equals REF in each (`C_21`'s removal order may vary in REF too; record that).
- [x] Repro variants that already matched curl still match.

## Notes

- Split from BL-3978 (cost cap).
- Continues the stashed work of BL-3978.
- 2026-10-29 rerun (Windows, after BL-4075): REF and PORT equal for `C_20`, `C_21` (removal order `S1 S2 S3` in both this time), `C_74`, `C_75` (PORT `C_75` matched in 3 of 4 runs; one run had an extra `T1` between `S1=1` and `S1=4`). `C_65` now differs in 4 of 4 PORT runs: `T0 add S1=2 T0 kick S1=4 T-1`, `S1:NOT-A-SOCKET`, poll errors, against REF `T0 add S1=2 T-1 kick S1=4`, `S1:real`. Engine fix filed as BL-4086 (outside this task's touches); rerun the probe and record the results here once it is Done. Probe source and the earlier outputs are in `.cgr034probe/` (uncommitted, stashed).

- 2026-10-29 rerun after BL-4086 (Windows): REF and PORT equal in all five cases; PORT `C_65`, `C_75` and `C_21` matched in 4 of 4 runs. REF `C_21` removal order was `S2 S1 S3` this time, recorded as completion-order variance. The variants that already matched (`C_20`, `C_74`) still match. Docs only, so no build or quality run; probe source in the document is unchanged (equal to `.cgr034probe/probe.cs`).

## Log

- 2026-10-29: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: e42eb3bf b084e04d.
- 2026-10-29: Doing -> Backlog. Waits on BL-4086: CGR-034 C_65 now differs (PORT closes a refused connect's socket before its REMOVE callback); C_20, C_21, C_74, C_75 match REF
- 2026-10-29: Backlog -> Doing. Criteria at claim: e42eb3bf b084e04d.
- 2026-10-29: Doing -> Done. CGR-034 probe rerun: PORT equals REF in C_20, C_21, C_65, C_74, C_75; results recorded
