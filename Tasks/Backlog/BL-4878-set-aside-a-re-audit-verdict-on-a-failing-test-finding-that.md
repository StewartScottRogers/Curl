---
id: BL-4878
title: Set aside a re-audit verdict on a failing-test finding that passes on the clean commit
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Audit/Tools/Write-AuditFindings.ps1]
lane: no
requirement: none
created: 2026-11-05
completed:
---
# BL-4878 — Set aside a re-audit verdict on a failing-test finding that passes on the clean commit

## Goal

A re-audit that says "reproduces: yes" on a failing-test finding whose reproduction is one `dotnet test <project> --filter "<filter>"` command is checked by the runner's clean rerun of that command, so a failure caused only by a planted defect is set aside instead of keeping the finding open.

## Context

AF-0665 (`Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine`, Curl.Engine.UnitTests) was filed in audit 20261104-144231 and "still reproduced" in audit 20261105-081937. Both audits planted PD-303, which deletes the `--path-as-is` row from `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:332` (see each run's manifest.json), and the quality auditor ran the Engine test in the planted tree. On the real tree the test passes (BL-4866 Notes). The same false positive was AF-0366 before.

`Audit/Tools/Write-AuditFindings.ps1` already reruns a NEW finding's single `dotnet test` reproduction on the clean commit and sets it aside when it passes there (BL-3503). A RE-AUDIT verdict gets no such rerun: `Get-OverlappingPlant` sets it aside only when the evidence names the planted file, or the location is that file, or (mutation reproductions only) the plant is in the project's closure. The 2026-11-05 evidence cited `TransferContextFactoryTests.cs:943`, not `CommandLineOptionTable.cs`, so nothing set it aside, and a Re-fix task (BL-4866) was filed for a defect that does not exist.

## Acceptance criteria

- [ ] For a re-audit "reproduces: yes" on a finding whose reproduction is exactly one `dotnet test <project> --filter "<filter>"`, the runner reruns it on the clean audited commit (as BL-3503 does for new findings); if the tests run and none fails, the verdict is recorded "not re-audited | ... set aside" (naming the overlapping plant when one is found) and no Re-fix task follows from it.
- [ ] `Write-AuditFindings.ps1 -SelfTest` has a case for it, built on AF-0665's shape, and passes under Windows PowerShell 5.1 and pwsh 7.

## Notes

## Log

- 2026-11-05: Created.
