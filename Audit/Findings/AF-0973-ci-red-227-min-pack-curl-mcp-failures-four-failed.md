---
id: AF-0973
title: CI red 227 min: Pack Curl.Mcp failures, four failed runs with no filed/covered trace line; first green run 16:34 Arizona time
auditor: process
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: process:logs:run-44942529813-44946270671-44946492351-44952616925:ci-red
reproduction: none
task: BL-5744
tasks: BL-5744
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0973 - CI red 227 min: Pack Curl.Mcp failures, four failed runs with no filed/covered trace line; first green run 16:34 Arizona time

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261121-113211.log:45`: CI red 227 min: Pack Curl.Mcp failures, four failed runs with no filed/covered trace line; first green run 16:34 Arizona time.

## Evidence

Location: `logs/DarkFactory-20261121-113211.log:45`

Red spell 19:47:17Z to 23:34:30Z (227.22 min), started by run 44941565490. That run was filed within 7 minutes as BL-5710, BL-5711 and BL-5712 (log line 34). Runs 44942529813, 44946270671, 44946492351 and 44952616925 ended at 12:59:49, 13:47:30, 14:01:42 and 15:29:04 Arizona time. Their trace lines (log lines 45, 109, 115, 130) carry no filed, covered or passed since. Next green run 44961136795 ended 16:34:30, so more than 30 minutes passed for all four. The shift ended its lanes at 13:29 and then only polled the board until 17:40.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path <log folder>\DarkFactory-20261121-113211.log -Pattern '44942529813|44946270671|44946492351|44952616925'
```

- Expected: Each run has a ci trace line with filed, covered or passed since within 30 minutes of the run ending.
- Actual: Only truncated 'failure on <sha>' lines; no filed or covered. Green only at 16:34:30.

## Re-audits

## Log

- 2026-11-22: filed proposed.
