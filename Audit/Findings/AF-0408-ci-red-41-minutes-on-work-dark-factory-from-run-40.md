---
id: AF-0408
title: CI red 41 minutes on work/dark-factory from run 40008612979 (three VerifyPeer_CaCertificateFileHoldingOnly* partial-chain tests)
auditor: process
severity: Low
status: accepted
reason: 
key: process:logs:VerifyPeer_CaCertificateFileHoldingOnlyAnIntermediateInTheOpenSslBuild:ci-red
reproduction: none
task: none
tasks:
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0408 - CI red 41 minutes on work/dark-factory from run 40008612979 (three VerifyPeer_CaCertificateFileHoldingOnly* partial-chain tests)

## Summary

Low finding from the process auditor at `logs/ci-runs.json`: CI red 41 minutes on work/dark-factory from run 40008612979 (three VerifyPeer_CaCertificateFileHoldingOnly* partial-chain tests).

## Evidence

Location: `logs/ci-runs.json`

ciRedSpells: 2026-10-23T19:28:00Z to 20:08:59Z, 40.98 min, run 40008612979 on a07ed80b. The coordinator filed BL-3346, BL-3347 and BL-3348 at 12:34:23 local, 6 min after the red (DarkFactory-20261023-110315.log:52-55). L1 fixed them between 12:35:51 and 12:42:58 (BL-3347 closed as 'Already fixed by BL-3346'). A second failing run, 40011412660 on 9e379e12, was pushed before the fix. Green at 13:08 local.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Measure-FactoryProcess.ps1 -Since 2026-10-23 -LogRoot Z:\repos\Curl.audit\20261023-232221\logs -CiRunsJson Z:\repos\Curl.audit\20261023-232221\logs\ci-runs.json -OutFile $env:TEMP\process.json; (Get-Content $env:TEMP\process.json -Raw | ConvertFrom-Json).ciRedSpells | Where-Object runId -eq 40008612979
```

- Expected: no ciRedSpells entry over 30 minutes
- Actual: start 2026-10-23T19:28:00Z end 20:08:59Z minutes 40.98 runId 40008612979

## Re-audits

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
