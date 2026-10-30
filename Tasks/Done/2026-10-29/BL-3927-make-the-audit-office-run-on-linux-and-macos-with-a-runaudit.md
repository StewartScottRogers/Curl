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
completed: 2026-10-29
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

- [x] `Audit/RunAudit.sh` exists, is executable, has LF line ends, needs only `pwsh` and passes every argument through to `Audit/RunAudit.ps1`.
- [x] `Audit/RunAudit.sh -DryRun` in WSL Ubuntu (or a Linux runner) prints the same plan as `Audit\RunAudit.cmd -DryRun` on Windows for the same commit (both in the Log).
- [x] Every `Audit/Tools/*.ps1` and `Audit/PlantedDefects/*.ps1` script that has a self-test or `-Help` runs it under `pwsh` on Linux without error.
- [x] `CLAUDE.md`'s "Audit office" section names `Audit/RunAudit.sh` as the Linux and macOS way to run an audit.
- [x] The change reached `master` through the audit-branch pull request with CI green on Windows, Linux and macOS.

## Notes

## Log

- 2026-10-27: Created.
- 2026-10-29: Backlog -> Doing. Criteria at claim: f168b8e2 afe00701 4a5cc2b2 de4f6030 d0e9ac37.
- 2026-10-29: Audit/RunAudit.sh added (mode 100755, LF, needs only pwsh, "$@" passed through). Dry run on commit 6255397567e8 with -Seed 1: WSL Ubuntu (pwsh 7.4.6, a clean LF clone) and Windows (RunAudit.cmd, -AlongsideShift as a shift was running) both print the same 22 lines once the repo root, the run stamp and the path separator are normalised; 0 differences.
- 2026-10-29: Self-tests under pwsh on Linux (clean LF checkout): RunAudit, Find-BooleanNameMismatches, Find-WeakTests, Measure-FactoryProcess, New-TasksFromAcceptedFindings, Test-AuditDue, Write-AuditFindings, Write-AuditScorecard exit 0. Invoke-MutationTest needs dotnet, which WSL lacks; CI's new audit-self-tests job runs it on ubuntu-latest and macos-latest with .NET. Get-AuditorFingerprint and Read-PlantedCatalogue (no self-test) run on Linux too; the fingerprint is identical on both platforms. Five checks in Write-AuditFindings/Write-AuditScorecard failed on Windows CRLF checkouts already on master; they now accept \r. PR #253.
- 2026-10-29: PR #253 merged into master (merge a9f1a8149); CI green on Windows, Linux and macOS, including Audit self-tests on all three (Invoke-MutationTest ran on ubuntu-latest and macos-latest). A hung quality shard 3 (Curl.Console.UnitTests, not touched here) was rerun and passed. master merged into work/dark-factory.
- 2026-10-29: Doing -> Done. PR #253 merged with CI green on all three platforms
