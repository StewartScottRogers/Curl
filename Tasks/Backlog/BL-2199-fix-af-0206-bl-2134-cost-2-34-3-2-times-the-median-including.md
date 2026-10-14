---
id: BL-2199
title: Fix AF-0206: BL-2134 cost $2.34, 3.2 times the median, including a repair run after its own run left test1462 red
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [RunDarkFactory.ps1]
requirement: none
created: 2026-10-14
completed:
---
# BL-2199 — Fix AF-0206: BL-2134 cost $2.34, 3.2 times the median, including a repair run after its own run left test1462 red

## Goal

The defect the audit office reported as AF-0206 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0206 (Low, process auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0206-bl-2134-cost-2-34-3-2-times-the-median-including-a.md`.

Location: `logs/DarkFactory-20261013-095305-L1.log:22`

Location: `logs/DarkFactory-20261013-095305-L1.log:22`

Measure-FactoryProcess.ps1: BL-2134 costUsd 2.3368, 10.93 minutes, against a $0.73 median. The cost is two runs. The main run (BL-2134-20261013-095305-L1.jsonl) took 38 turns and $1.55, with 25 Bash calls, 19 of them slice reads. The repair run (BL-2134-20261013-095305-L1-repair.jsonl) took 22 turns and $0.78. The lane log shows 12:14:37 'flaky? fast tests red (Curl.Conformance.UnitTests: test1462)' and 12:15:00 'repair fast tests failed twice', then green at 12:19:20 after the repair took test1462 off the ratchet (5dbe71d11). test1462 already belonged to BL-2136, filed for the same CI run, so the repair work overlapped another task's scope.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-13 -LogRoot Z:\repos\Curl.audit\20261013-182203\logs -CiRunsJson Z:\repos\Curl.audit\20261013-182203\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).tasks | Where-Object id -eq 'BL-2134'; Select-String -Path Z:\repos\Curl.audit\20261013-182203\logs\DarkFactory-20261013-095305-L1.log -Pattern 'BL-2134 (flaky|repair|end)'
```

- Expected: No task over 3 times the median costUsd (about $2.19).
- Actual: BL-2134 costUsd 2.3368. The log shows two 'end' lines (38 and 22 turns) around 'flaky?' and two 'repair' lines.

The finding closes only when a later re-audit by the process auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
