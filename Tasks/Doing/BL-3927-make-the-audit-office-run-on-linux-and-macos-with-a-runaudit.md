---
id: BL-3927
title: Make the audit office run on Linux and macOS, with a RunAudit.sh launcher
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [CLAUDE.md, Audit/RunAudit.ps1, Audit/RunAudit.sh, Audit/Tools, Audit/PlantedDefects]
lane: no
requirement: none
created: 2026-10-27
completed:
---
# BL-3927 — Make the audit office run on Linux and macOS, with a RunAudit.sh launcher

## Goal

`Audit/RunAudit.sh -DryRun` prints the same plan on Linux and macOS as `Audit\RunAudit.cmd -DryRun`
does on Windows, and a real audit can run there under `pwsh`, opening its tabs in herdr.

## Context

Part of BL-3934 (the whole repository workable on Linux and macOS). Interactive only: `Audit/` is
an audit path, so the work is done on the `audit` branch and merged under the standing exception.
Files with Windows-only constructs found by grep: `Audit/RunAudit.ps1`,
`Audit/PlantedDefects/Read-PlantedCatalogue.ps1` and, under `Audit/Tools/`,
`Get-AuditorFingerprint.ps1`, `Invoke-DifferentialConformance.ps1`, `Invoke-MutationTest.ps1`,
`Measure-FactoryProcess.ps1`, `New-TasksFromAcceptedFindings.ps1`, `Test-TransfersUnderLoad.ps1`,
`Write-AuditFindings.ps1` and `Write-AuditScorecard.ps1`; check the other `.ps1` files there too.
Typical fixes: choose `powershell`/`pwsh` by platform, `Join-Path` instead of backslashes,
`Get-CimInstance` replaced by a cross-platform reading, the reference curl being the OpenSSL build
off Windows (`Set-DifferentialEnvironment.sh` already finds it). `Audit/RunAudit.sh` is a new
launcher, like `RunDarkFactory.sh`, that runs `RunAudit.ps1` under `pwsh` with the same arguments.
Audit/Guard is BL-3926's.

## Acceptance criteria

- [ ] `Audit/RunAudit.sh` exists, is executable, has LF line ends, needs only `pwsh` and passes every argument through to `Audit/RunAudit.ps1`.
- [ ] `Audit/RunAudit.sh -DryRun` in WSL Ubuntu (or a Linux runner) prints the same plan as `Audit\RunAudit.cmd -DryRun` on Windows for the same commit (both in the Log).
- [ ] Every `Audit/Tools/*.ps1` and `Audit/PlantedDefects/*.ps1` script that has a self-test or `-Help` runs it under `pwsh` on Linux without error.
- [ ] `CLAUDE.md`'s "Audit office" section names `Audit/RunAudit.sh` as the Linux and macOS way to run an audit.
- [ ] The change reached `master` through the audit-branch pull request with CI green on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f168b8e2 afe00701 4a5cc2b2 de4f6030 d0e9ac37.
