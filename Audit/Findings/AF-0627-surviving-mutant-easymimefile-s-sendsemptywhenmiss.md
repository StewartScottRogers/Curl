---
id: AF-0627
title: Surviving mutant: EasyMimeFile's SendsEmptyWhenMissing can default to true, so a missing -F file part is sent empty, and no test fails
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-04_0643.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Api.UnitLibrary/Options/Http/EasyMimeFile.cs:EasyMimeFile-false:surviving-mutant
reproduction: mutation Curl.Api.UnitLibrary/Options/Http/EasyMimeFile.cs:12:false
task: BL-4806
tasks: BL-4806
found: 2026-11-03
found-at: 0ae5ce3f5e65976edeb9facb28b93fd3f4dcdbc8
scorecard: 2026-11-03_2203.md
duplicate-of:
closed: 2026-11-04
closed-how: mechanical
closed-by: 2026-11-04_0643.md
---
# AF-0627 - Surviving mutant: EasyMimeFile's SendsEmptyWhenMissing can default to true, so a missing -F file part is sent empty, and no test fails

## Summary

High finding from the quality auditor at `Curl.Api.UnitLibrary/Options/Http/EasyMimeFile.cs:12`: Surviving mutant: EasyMimeFile's SendsEmptyWhenMissing can default to true, so a missing -F file part is sent empty, and no test fails. Reported by an auditor flagged unreliable in 2026-11-03_2203.md.

## Evidence

Location: `Curl.Api.UnitLibrary/Options/Http/EasyMimeFile.cs:12`

Invoke-MutationTest -Library Curl.Api.UnitLibrary -MaxMutants 25 -Seed 221035: survived, 'public sealed record EasyMimeFile(string Path, bool SendsEmptyWhenMissing = false)' -> '= true'. Curl.Console/EasyOptionsFromCommandLine.cs:586 (new EasyMimeFile(part.Content)) and Curl.Engine.UnitLibrary/FormPost.cs:184 (new EasyMimeFile(file)) rely on the default. With true, curl -F f=@missing and curl_formadd's file part send an empty part instead of failing with exit 26. No Curl.Api test pins the default.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Api.UnitLibrary/Options/Http/EasyMimeFile.cs:12:false -Member EasyMimeFile -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-04 | 2026-11-04_0643.md | reproduces: no | Ran the -Site command: outcome killed, resolvedLine 12. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-04: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-04: accepted -> closed. Re-audit 2026-11-04_0643.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
