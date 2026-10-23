---
id: AF-0388
title: CI red for 40 minutes on work/dark-factory: Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason failed on Linux and macOS
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0388 - CI red for 40 minutes on work/dark-factory: Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason failed on Linux and macOS

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red for 40 minutes on work/dark-factory: Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason failed on Linux and macOS.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: start 2026-10-23T05:36:44Z, end 06:16:53Z, 40.15 minutes, run 39903809116 (failure on 560a15dae), stillRed false. The cause is commit 109584a33 (BL-3183, 21:47 Arizona), which added 'ssl-sessions' to the RefusedForAnotherReason rows of CommandLineBlankArgumentTests. On Linux and macOS (OpenSSL build) curl refuses --ssl-sessions '' as blank: 'Expected collection to not contain the specified element.' The failure showed only at 560a15da because the CI runs in between were cancelled by later pushes. Timeline (Arizona): red at 22:36:44. BL-3203 filed by the CI watch at 22:39:46 (3 min). L5 claimed it at 22:41:47 and pushed the fix f72400608 (OS-pinned tests) at 22:45:46 (9 min after red). Run 39906620565 on cbfaee49, started before the fix, also failed. Green came from run 39907812652 (cd6ec6b2c), which started 22:48:33 and ended 23:16:53. About 31 of the 40 minutes were the length of a full CI run after the fix. Lanes test only on Windows, so a platform-specific test row went untested until CI.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261023-061815\logs -CiRunsJson Z:\repos\Curl.audit\20261023-061815\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells
```

- Expected: No red spell over 30 minutes.
- Actual: start 2026-10-23T05:36:44Z, end 2026-10-23T06:16:53Z, minutes 40.15, runId 39903809116, stillRed False

## Re-audits

## Log

- 2026-10-23: filed proposed.
