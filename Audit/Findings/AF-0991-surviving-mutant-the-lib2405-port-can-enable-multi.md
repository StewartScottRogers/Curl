---
id: AF-0991
title: Surviving mutant: the lib2405 port can enable multiplexing for the HTTP/1 runs and not the HTTP/2 run (== -> !=)
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-25_1306.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Conformance.LibTest.UnitLibrary/Lib2405Run.cs:TransferAsync-eq:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/Lib2405Run.cs:134:==
task: BL-5793
tasks: BL-5793
found: 2026-11-22
found-at: 94c9744bc919995aff928a0378582601ddbbe990
scorecard: 2026-11-22_1817.md
duplicate-of:
closed: 2026-11-25
closed-how: mechanical
closed-by: 2026-11-25_1306.md
---
# AF-0991 - Surviving mutant: the lib2405 port can enable multiplexing for the HTTP/1 runs and not the HTTP/2 run (== -> !=)

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/Lib2405Run.cs:134`: Surviving mutant: the lib2405 port can enable multiplexing for the HTTP/1 runs and not the HTTP/2 run (== -> !=).

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/Lib2405Run.cs:134`

Seeded mutation run: 'if (use == Lib2405.UseHttp2Multiplex)' -> 'if (use != Lib2405.UseHttp2Multiplex)' survived. The branch sets CURLMOPT_PIPELINING to multiplex; the mutant inverts which lib2405 variants multiplex. No test checks the multi option set for each variant.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/Lib2405Run.cs:134:== -Member TransferAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-25 | 2026-11-25_1306.md | reproduces: no | Ran the -Site command: line 134, 'use == Lib2405.UseHttp2Multiplex' -> '!=', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-22: filed proposed.
- 2026-11-25: accepted -> closed. Re-audit 2026-11-25_1306.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
