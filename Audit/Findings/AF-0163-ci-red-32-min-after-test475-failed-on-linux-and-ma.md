---
id: AF-0163
title: CI red 32 min after test475 failed on Linux and macOS from BL-1907's commit 7b6794f4 (run 38047662088)
auditor: process
severity: Low
status: closed
reason: Stewart rejected it on 2026-10-11: the red spell was prompt filing and claiming, not a factory defect (BL-2056).
key: process:logs:test475:ci-red
reproduction: none
task: BL-2056
tasks: BL-2056
found: 2026-10-11
found-at: 22f394602532a6922adbc7fa91b3739b62d2112a
scorecard: 2026-10-11_0051.md
duplicate-of:
closed: 2026-10-11
closed-how: stewart
closed-by: stewart
---
# AF-0163 - CI red 32 min after test475 failed on Linux and macOS from BL-1907's commit 7b6794f4 (run 38047662088)

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261010-022215.log:31`: CI red 32 min after test475 failed on Linux and macOS from BL-1907's commit 7b6794f4 (run 38047662088).

## Evidence

Location: `logs/DarkFactory-20261010-022215.log:31`

ciRedSpells[1]: start 2026-10-10T11:19:01Z (04:19 local), end 11:51:01Z, 32 min, runId 38047662088, commit 7b6794f42 'feat(conformance): serve active-mode FTP and uploads in the stand-in' (BL-1907, lane L1). Coordinator at 04:21:53: 'filed: regression test475 on Linux+macOS, run 38047662088' as BL-1957, 3 min after the red. Runs 38048526000 and 38049074289 stayed red. The shift had one lane, which was busy with BL-1908 and BL-1913, so the fix was claimed only at 04:37:45 (DarkFactory-20261010-022215-L1.log:135), 16 min after filing. The fix b1124f69 landed at 04:43:49 and CI was green at 04:51:01. Lanes test only on Windows, so this failure could not be caught before the push. Also, the filed ID BL-1957 collided with a lane-made task ('renum BL-1957 -> BL-1958' at 04:27:29).

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-10 -LogRoot Z:\repos\Curl.audit\20261011-005116\logs -CiRunsJson Z:\repos\Curl.audit\20261011-005116\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 38047662088; Select-String -Path Z:\repos\Curl.audit\20261011-005116\logs\DarkFactory-20261010-022215.log -Pattern 'run 38047662088|filed: regression'
```

- Expected: No spell over 30 minutes.
- Actual: minutes 32 runId 38047662088; '04:21:53 BL-1957 ci filed: regression test475 on Linux+macOS, run 38047662088'

## Re-audits

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
- 2026-10-11: accepted -> closed. Stewart rejected it on 2026-10-11: the red spell was prompt filing and claiming, not a factory defect (BL-2056).
