---
id: BL-109
title: Report the curl Cobertura package as Curl.Console in Measure-CodeQuality.ps1
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Measure-CodeQuality.ps1]
requirement: none
created: 2026-09-26
completed:
---
# BL-109 — Report the curl Cobertura package as Curl.Console in Measure-CodeQuality.ps1

## Goal

`Measure-CodeQuality.ps1` measures Curl.Console and reports it under the name
`Curl.Console`, so its row appears in the library table and its failing members are
listed.

## Context

- `Measure-CodeQuality.ps1`, `Test-IsProductionAssembly` (around line 131-136), accepts
  a Cobertura package named `*.UnitLibrary` or exactly `Curl.Console`.
- `Curl.Console/Curl.Console.csproj` sets `<AssemblyName>curl</AssemblyName>`, so the
  assembly is `curl.dll` and its Cobertura `<package name="...">` is `curl`, not
  `Curl.Console`. The filter therefore drops it, and Curl.Console silently vanishes
  from the report's table and failing-member list.
- The root `CLAUDE.md` holds Curl.Console to the same 100% line, 100% branch,
  complexity 10 and CRAP 30 gates as every `*.UnitLibrary`, so the audit must include it.
- Observed 2026-09-26 while working BL-106: the report listed 10 libraries and no
  Curl.Console row.
- Suggested shape: in the merge loop (section 2), map the package name `curl` to
  `Curl.Console` before the production-assembly test, and use the mapped name for the
  method key and the `Assembly` field, so the table, the failing-member heading and the
  `-Library` filter all see `Curl.Console`. Keep the mapping to that one exact name;
  do not change how any other package is named or filtered. Update the `.PARAMETER
  Library` help text if its wording no longer matches.

## Acceptance criteria

- [ ] Running `powershell -NoProfile -File Measure-CodeQuality.ps1` from the
      repository root prints a row beginning `| Curl.Console |` in the `## Libraries`
      table.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1 -SkipTestRun -Library Curl.Console`
      prints a `## Libraries` table whose only row is `| Curl.Console |`.
- [ ] No row named `| curl |` appears in the report.
- [ ] Every other library's row (name, Line %, Branch %, Members, Failing, Worst CRAP)
      is identical to a run of the unmodified script against the same Cobertura files
      (compare with `-SkipTestRun` before and after the change).
- [ ] Only `Measure-CodeQuality.ps1` is changed.

## Notes

## Log

- 2026-09-26: Created.
