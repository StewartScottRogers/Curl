---
id: AF-0961
title: CI red 227 min (run 44941565490 onward): Pack Curl.Mcp failures left unfixed, four failed runs have no filed/covered trace line
auditor: process
severity: Low
status: closed
reason: Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
key: process:logs:pack-curl-mcp-spell-2026-11-21T19-47:ci-red
reproduction: none
task: BL-5730
tasks: BL-5730
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed: 2026-11-22
closed-how: reliable-reaudit
closed-by: 2026-11-22_1351.md
---
# AF-0961 - CI red 227 min (run 44941565490 onward): Pack Curl.Mcp failures left unfixed, four failed runs have no filed/covered trace line

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 227 min (run 44941565490 onward): Pack Curl.Mcp failures left unfixed, four failed runs have no filed/covered trace line. Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells[1]: start 2026-11-21T19:47:17Z, end 23:34:30Z, 227.22 min, runId 44941565490. Failed runs in the spell: 44941565490 (ended 12:47 MST), 44942529813 (12:59), 44943840882 (13:12), 44946270671 (13:47), 44946492351 (14:01), 44952616925 (15:29). Filed within 30 min: 44941565490 (BL-5710/5711/5712 at 12:54:04) and 44943840882 (BL-5713 at 13:18:54). No filed/covered/passed-since line names 44942529813, 44946270671, 44946492351 or 44952616925 in DarkFactory-20261121-113211.log; their lines are truncated at 'failure on <sha>: job Pack Curl.Mcp ...~', and no line in the log copy contains 'covered'. Next green run ended 16:34 MST (44961136795). The three CI fix tasks were requeued interactive-only (12:59:26, 13:00:52, 13:28:53) because the fix is in a guard file or .github. Caveat: the truncated lines may have carried a 'covered' verdict that the log does not show.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-11-21 -LogRoot <log folder> -CiRunsJson <log folder>\ci-runs.json -OutFile process.json; Select-String -Path <log folder>\DarkFactory-20261121-113211.log -Pattern '44942529813|44946270671|44946492351|44952616925'
```

- Expected: Each failed run has a filed, covered or passed-since trace line within 30 minutes of its end.
- Actual: Spell of 227.22 min; the four runs have only plain 'failure on' lines with no filed/covered/passed-since wording.

## Re-audits

- 2026-11-22 | 2026-11-22_0210.md | reproduces: yes | Ran the Measure-FactoryProcess.ps1 command: spell 19:47:17Z to 23:34:30Z is 227.22 min, started by run 44941565490, still reported. The Select-String over DarkFactory-20261121-113211.log shows the four runs 44942529813, 44946270671, 44946492351 and 44952616925 only in truncated 'failure on' lines and 'running ... no failing test' lines, with no filed, covered or passed since. Their ends (19:59:49Z, 20:47:30Z, 21:01:42Z, 22:29:04Z) are each more than 30 minutes before the next green run (23:34:30Z). The window holds a shift that finished tasks, so the verdict stands.
- 2026-11-22 | 2026-11-22_1351.md | reproduces: no | DarkFactory-20261121-113211.log is not in the log copy, so the Select-String found nothing. Under the 2026-11-22 window (28 tasks done) the CI red rule is not crossed: ciRedMinutes 18.83; run 45059360804 was green again within 19 minutes; run 45065328172 was filed as BL-5760 at 13:43:38. No Pack Curl.Mcp failure appears in the window.

## Log

- 2026-11-21: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces.
