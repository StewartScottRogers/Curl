---
id: AF-0150
title: CI red 33.92 min after test4001 failed on macOS from BL-1947's commit (run 38030548658)
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-10-13_0925.md: the reproduction no longer reproduces.
key: process:logs:CI-run-38030548658:ci-red
reproduction: none
task: BL-2118
tasks: BL-1969, BL-2045, BL-2055, BL-2118
found: 2026-10-10
found-at: 1b27494521dec4bdaa3fe60c8dc7a3fc73874253
scorecard: 2026-10-10_0123.md
duplicate-of:
closed: 2026-10-13
closed-how: reliable-reaudit
closed-by: 2026-10-13_0925.md
---
# AF-0150 - CI red 33.92 min after test4001 failed on macOS from BL-1947's commit (run 38030548658)

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261009-213949.log`: CI red 33.92 min after test4001 failed on macOS from BL-1947's commit (run 38030548658).

## Evidence

Location: `logs/DarkFactory-20261009-213949.log`

ciRedSpells: start 2026-10-10T06:25:57Z, end 06:59:52Z, 33.92 min, runId 38030548658, failure on 1c6aa1ac (BL-1947, Curl.Cli quality-gate refactor); test4001 failed on macOS. At 23:27:28 PDT the coordinator logged 'test4001 on macOS only, waiting for the next run'. It filed BL-1949 only at 23:43:05 PDT, after run 38031396189 also failed: 17 minutes after the red. BL-1949 was claimed at 23:43:20, fixed in 336f2af62 at 23:47:49 (macOS server lacks TLS 1.3), and run 38032497110 on 2cf92854 was green at 06:59:52Z. Runs 38031906825 on 3bc50029 also failed in between. No master merge during the spell: merges at 21:39 and 00:44 PDT.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-09 -LogRoot Z:\repos\Curl.audit\20261010-012326\logs -CiRunsJson Z:\repos\Curl.audit\20261010-012326\logs\ci-runs.json -OutFile $env:TEMP\process.json; Select-String -Path Z:\repos\Curl.audit\20261010-012326\logs\DarkFactory-20261009-213949.log -Pattern 'run 38030548658|filed BL-1949'
```

- Expected: No red spell over 30 minutes
- Actual: spell 06:25:57Z-06:59:52Z, 33.92 min; 23:27:28 'waiting for the next run'; 23:43:05 'filed BL-1949'

## Re-audits

- 2026-10-11 | 2026-10-11_0051.md | reproduces: yes | The reproduction on the 20261010-012326 logs shows 'run 38030548658 failure on 1c6aa1ac: test4001 on macOS only' and 'filed BL-1949' at 23:43:05. The same cause, a test that passes in the Windows-only lane tests but fails on Linux/macOS from a lane's commit and keeps CI red over 30 min, crosses again in this window. Run 38047662088: test475 failed on Linux+macOS from BL-1907's commit 7b6794f4, and CI was red 32 min (11:19:01Z to 11:51:01Z) until BL-1957's fix b1124f69. The same 38030548658 spell (33.92 min) also reappears in this window's ciRedSpells.
- 2026-10-11 | 2026-10-11_1638.md | reproduces: yes | The CI red rule crosses again with the same cause, a test failing off Windows on a factory commit. Run 38178740830 (test303 on Linux, commit 0d18262, BL-2076's completion) started a spell at 22:12:33Z, 69 min and still red at the window's end. BL-2093 was filed 14.3 min after the failure and was Done at 22:34Z. Later failures on b362b84 and c811b9e named no test and were not filed. ciRedMinutes 169.97.
- 2026-10-12 | 2026-10-12_1653.md | reproduces: yes | The original reproduction still shows its incident in the old logs: spell 38030548658, 33.92 min. Judged by mechanism over this window (-Since 2026-10-11, 14 tasks done), the same pattern happened again. Run 38178740830 went red when test303 failed on Linux only; lanes test only on Windows. The spell lasted 301.1 min (22:12:33Z to 03:13:39Z), well over the 30-minute rule. Unlike AF-0150's case, test303 was filed within 14 min (BL-2093). The spell stayed red because four later Quality-gates failures went unfiled.
- 2026-10-13 | 2026-10-13_0925.md | reproduces: no | Ran the reproduction. In the old window it still shows the 33.92-min spell from run 38030548658 and 'test4001 regression, filed BL-1949'. Judged by mechanism over this window (Since 2026-10-12, 4 shifts, 10 tasks Done): ciRedMinutes 0, ciRedSpells empty, every non-cancelled run on work/dark-factory since 2026-10-12T02:50Z is a success, and no 'waiting for the next run' line appears. No failure went unfiled or was filed late.

## Log

- 2026-10-10: filed proposed.
- 2026-10-10: proposed -> accepted.
- 2026-10-13: accepted -> closed. Re-audit 2026-10-13_0925.md: the reproduction no longer reproduces.
