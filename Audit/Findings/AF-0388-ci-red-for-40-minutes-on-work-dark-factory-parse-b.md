---
id: AF-0388
title: CI red for 40 minutes on work/dark-factory: Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason failed on Linux and macOS
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces.
key: process:logs:Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason:ci-red
reproduction: none
task: BL-3365
tasks: BL-3365
found: 2026-10-23
found-at: 32b1dda228a5fded8978ed4ee514a932b0728722
scorecard: 2026-10-23_0618.md
duplicate-of:
closed: 2026-10-24
closed-how: reliable-reaudit
closed-by: 2026-10-24_0845.md
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

- 2026-10-23 | 2026-10-23_2322.md | reproduces: yes | The given command shows the old 40.15-min spell (run 39903809116). Over this window ciRedMinutes = 319.05, with spells over 30 min from failing tests on Linux/macOS: 37.45 min (run 39986660283), 40.98 (40008612979), 76.97 (40021720442, macOS), 50.33 (40034290028, test1173 on Linux and macOS), plus 90 min (90941437168).
- 2026-10-24 | 2026-10-24_0845.md | reproduces: no | Re-audited by mechanism over this audit's window. With -Since 2026-10-23, the ciRedSpells the tool lists all ended by 2026-10-24T00:36:24Z, before this log folder's first line (2026-10-23 20:10 Arizona, 03:10Z). Those spells are the incidents of AF-0407 to AF-0410, plus a 23.32-minute spell from run 40040045270. With -Since 2026-10-24 the tool gives ciRedMinutes 0 and ciRedSpells []. Every CI run in ci-runs.json from 01:38Z to 14:09Z on 2026-10-24 is a success or cancelled, so no test failing only on Linux and macOS turned CI red in this window.

## Log

- 2026-10-23: filed proposed.
- 2026-10-23: proposed -> accepted.
- 2026-10-24: accepted -> closed. Re-audit 2026-10-24_0845.md: the reproduction no longer reproduces.
