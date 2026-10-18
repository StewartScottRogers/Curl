---
id: AF-0268
title: BL-2550 cost $7.39, 3.04 times the median: 24 of its 51.6 minutes went on two shell loops reading 2,077 upstream test files one by one
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-2550:cost-outlier
reproduction: none
task: BL-2779
tasks: BL-2779
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0268 - BL-2550 cost $7.39, 3.04 times the median: 24 of its 51.6 minutes went on two shell loops reading 2,077 upstream test files one by one

## Summary

Low finding from the process auditor at `logs/DarkFactory-20261017-165834-L1.log:175`: BL-2550 cost $7.39, 3.04 times the median: 24 of its 51.6 minutes went on two shell loops reading 2,077 upstream test files one by one.

## Evidence

Location: `logs/DarkFactory-20261017-165834-L1.log:175`

Measure-FactoryProcess.ps1: BL-2550 claims 1, 52.98 min, $7.3905 (median $2.4326). DarkFactory-20261017-165834-L1.log:127 claims it at 18:06:33; its first file write is at 18:35:37; it ended at 18:58:14 with '120 turns, 51.6 min' (line 175). Peer port batches BL-2549 and BL-2551 took 26.6 and 22.6 min. In BL-2550-20261017-165834-L1.jsonl, two Bash calls looped 'for f in *.curltest' over Curl.Conformance.UnitTests/UpstreamTestData (2,077 files), running grep/tr/awk per file. They ran 01:07:25Z to 01:17:42Z and 01:17:55Z to 01:31:54Z, about 24 minutes with the session held open. A single Select-String or grep -l over the folder does the same lookup in seconds. The run ended with a quality FAIL ('unexpected EOF' in a bash eval) and an empty stash 0e12a7c4 (no tracked or untracked changes), so no work was lost.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-16 -LogRoot Z:\repos\Curl.audit\20261018-104525\logs -CiRunsJson Z:\repos\Curl.audit\20261018-104525\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | ? id -eq 'BL-2550'; Select-String -Path Z:\repos\Curl.audit\20261018-104525\logs\DarkFactory-20261017-165834-L1.log -Pattern 'BL-2550 (claim|write|end)' | Select-Object -First 3
```

- Expected: BL-2550 under 3 times the median cost ($7.30).
- Actual: BL-2550 1 52.98 7.3905 done; claim 18:06:33, first write 18:35:37.

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
