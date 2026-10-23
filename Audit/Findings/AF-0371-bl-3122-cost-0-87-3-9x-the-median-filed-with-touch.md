---
id: AF-0371
title: BL-3122 cost $0.87 (3.9x the median): filed with touches naming only Curl.Networking.UnitLibrary, requeued behind BL-3127, then rerun on Opus
auditor: process
severity: Low
status: proposed
reason:
key: process:logs:BL-3122:cost-outlier
reproduction: none
task: none
tasks:
found: 2026-10-22
found-at: 9ada2459e76878e927865fe930a3afac67191f22
scorecard: 2026-10-22_1902.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0371 - BL-3122 cost $0.87 (3.9x the median): filed with touches naming only Curl.Networking.UnitLibrary, requeued behind BL-3127, then rerun on Opus

## Summary

Low finding from the process auditor at `logs/BL-3122-20261022-143901-L2.jsonl`: BL-3122 cost $0.87 (3.9x the median): filed with touches naming only Curl.Networking.UnitLibrary, requeued behind BL-3127, then rerun on Opus.

## Evidence

Location: `logs/BL-3122-20261022-143901-L2.jsonl`

BL-3122 costUsd=0.87, claims=2, against a bar of 0.662. Run 1 (L2, 14:43:26): 8 turns, $0.296, result: 'I moved it back to Backlog before changing any code, because it waits on BL-3127.' Commit 6507ddd5b widened touches from [Curl.Networking.UnitLibrary] to add Curl.Networking.UnitTests/TcpConnectorQuicTests.UdpTunnel.cs. The requeue push waited until 14:54:44 (about 10 minutes). Run 2 (L6, 14:55:06): 21 turns, $0.574, including a refused Bash command ('Permission to use Bash with command head -5; cd .') and a by-hand mutant check (test 1/1, then 0/1). The fix was test-only ('needed only a new test, no production code change'), yet it ran on Opus ('names HTTP/3').

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-22 -LogRoot Z:\repos\Curl.audit\20261022-190247\logs -CiRunsJson Z:\repos\Curl.audit\20261022-190247\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-3122'
```

- Expected: costUsd at most 3 times the median (0.662).
- Actual: BL-3122 claims=2 minutes=5.52 costUsd=0.87 outcome=done

## Re-audits

## Log

- 2026-10-22: filed proposed.
