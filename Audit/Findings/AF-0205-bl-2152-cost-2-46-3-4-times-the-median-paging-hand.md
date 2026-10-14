---
id: AF-0205
title: BL-2152 cost $2.46, 3.4 times the median, paging HandBuiltTlsProvider.cs in slices after the read-once fix
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:BL-2152:cost-outlier
reproduction: none
task: BL-2198
tasks: BL-2198
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0205 - BL-2152 cost $2.46, 3.4 times the median, paging HandBuiltTlsProvider.cs in slices after the read-once fix

## Summary

Low finding from the process auditor at `logs/BL-2152-20261013-133412-L2.jsonl`: BL-2152 cost $2.46, 3.4 times the median, paging HandBuiltTlsProvider.cs in slices after the read-once fix.

## Evidence

Location: `logs/BL-2152-20261013-133412-L2.jsonl`

Measure-FactoryProcess.ps1: BL-2152 costUsd 2.4615, 7.5 minutes, against a $0.73 median. num_turns 62, cache_read_input_tokens 3,953,014. Tool calls: Edit 27, Bash 17, PowerShell 9, Read 8. All 8 Reads used an offset (for example HandBuiltTlsProvider.cs at offset 574, and TlsClientOptions.cs at offset 136), and 12 Bash calls sliced files, for example 'sed -n 560,660p HandBuiltTlsProvider.cs; sed -n 765,790p HandBuiltTlsProvider.cs; sed -n 805,820p ...'. BL-2152 was claimed at 13:58:41, after BL-2149's read-once prompt change (13:51:23), so sliced paging of a large file continued after that fix.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-13 -LogRoot Z:\repos\Curl.audit\20261013-182203\logs -CiRunsJson Z:\repos\Curl.audit\20261013-182203\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2152'; Select-String -Path Z:\repos\Curl.audit\20261013-182203\logs\BL-2152-20261013-133412-L2.jsonl -Pattern '"offset":[0-9]+|sed -n' | Measure-Object
```

- Expected: No task over 3 times the median costUsd (about $2.19).
- Actual: BL-2152 costUsd 2.4615, with 8 offset Reads and 12 sliced Bash reads.

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
