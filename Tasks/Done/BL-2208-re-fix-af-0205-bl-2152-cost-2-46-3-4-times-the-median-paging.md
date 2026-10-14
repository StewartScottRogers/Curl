---
id: BL-2208
title: Re-fix AF-0205: BL-2152 cost $2.46, 3.4 times the median, paging HandBuiltTlsProvider.cs in slices after the read-once fix
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-14
completed: 2026-10-14
---
# BL-2208 — Re-fix AF-0205: BL-2152 cost $2.46, 3.4 times the median, paging HandBuiltTlsProvider.cs in slices after the read-once fix

## Goal

The defect the audit office reported as AF-0205 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0205 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0205-bl-2152-cost-2-46-3-4-times-the-median-paging-hand.md`.

Re-fix: the earlier task(s) BL-2198 reached Done, and a later re-audit by the process auditor found the reproduction still reproduces:

- 2026-10-14 | 2026-10-14_0453.md | reproduces: yes | Original reproduction still prints BL-2152 costUsd 2.4615 and 14 offset/'sed -n' matches. In this window paging files in slices still drives a cost outlier: BL-2182 (lane 1, 20:52 PDT 2026-10-13) $4.80, 3.23x median, 19 'sed -n' trace lines and 2 offset Reads over 92 turns.

Location: `logs/BL-2152-20261013-133412-L2.jsonl`

Location: `logs/BL-2152-20261013-133412-L2.jsonl`

Measure-FactoryProcess.ps1: BL-2152 costUsd 2.4615, 7.5 minutes, against a $0.73 median. num_turns 62, cache_read_input_tokens 3,953,014. Tool calls: Edit 27, Bash 17, PowerShell 9, Read 8. All 8 Reads used an offset (for example HandBuiltTlsProvider.cs at offset 574, and TlsClientOptions.cs at offset 136), and 12 Bash calls sliced files, for example 'sed -n 560,660p HandBuiltTlsProvider.cs; sed -n 765,790p HandBuiltTlsProvider.cs; sed -n 805,820p ...'. BL-2152 was claimed at 13:58:41, after BL-2149's read-once prompt change (13:51:23), so sliced paging of a large file continued after that fix.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-13 -LogRoot Z:\repos\Curl.audit\20261013-182203\logs -CiRunsJson Z:\repos\Curl.audit\20261013-182203\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2152'; Select-String -Path Z:\repos\Curl.audit\20261013-182203\logs\BL-2152-20261013-133412-L2.jsonl -Pattern '"offset":[0-9]+|sed -n' | Measure-Object
```

- Expected: No task over 3 times the median costUsd (about $2.19).
- Actual: BL-2152 costUsd 2.4615, with 8 offset Reads and 12 sliced Bash reads.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Closed without a fix: AF-0205 describes a past run's cost, which BL-2218's rule now closes once its original task is Done.

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing. interactive session; closing without a fix under BL-2218
- 2026-10-14: Doing -> Done. Closed without a fix: AF-0205 describes a past run's cost, which BL-2218's rule now closes once its original task is Done
