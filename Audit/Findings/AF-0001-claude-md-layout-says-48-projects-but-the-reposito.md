---
id: AF-0001
title: CLAUDE.md layout says '48 projects' but the repository has 66
auditor: truthfulness
severity: Low
status: accepted
reason: Stewart accepted it on 2026-09-30.
key: truthfulness:CLAUDE.md:repository-layout-project-count:false-statement
task: BL-1065
found: 2026-09-30
found-at: d065d6d3507e2ed87905d40a24233af193913378
scorecard: 2026-09-30_1754.md
closed:
closed-by:
---
# AF-0001 - CLAUDE.md layout says '48 projects' but the repository has 66

## Summary

Low finding from the truthfulness auditor at `CLAUDE.md`: CLAUDE.md layout says '48 projects' but the repository has 66.

## Evidence

Location: `CLAUDE.md`

The Repository layout tree in CLAUDE.md says '...  <- 48 projects, one flat alphabetical run'. The repository root holds 66 Curl.* project directories (32 UnitLibrary, 32 UnitTests, Curl.Console, Curl.Console.UnitTests), each with a csproj, and Curl.slnx lists them all flat.

## Reproduction

Run from the repository root:

```powershell
(Select-String -Path CLAUDE.md -Pattern '\d+ projects, one flat').Line; (Get-ChildItem -Directory -Filter 'Curl.*').Count
```

- Expected: The count in CLAUDE.md equals the number of Curl.* project directories.
- Actual: CLAUDE.md says 48 projects; Get-ChildItem returns 66 directories.

## Re-audits

- 2026-10-02 | 2026-10-02_1400.md | reproduces: no | CLAUDE.md now reads '66 projects, one flat alphabetical run'; Get-ChildItem -Directory -Filter 'Curl.*' counts 66. Statement matches.
- 2026-10-03 | 2026-10-03_0623.md | reproduces: no | CLAUDE.md:202 reads '66 projects, one flat alphabetical run'. Counting directories matching 'Curl.*' gives 66. The statement matches the repository.
- 2026-10-03 | 2026-10-03_1233.md | reproduces: no | CLAUDE.md:202 now says '66 projects, one flat alphabetical run'; the repository has 66 Curl.* directories.

## Log

- 2026-09-30: filed proposed.
- 2026-09-30: proposed -> accepted. Stewart accepted it on 2026-09-30 (log written 2026-10-02, BL-1183).
