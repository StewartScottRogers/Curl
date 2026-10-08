---
id: BL-1671
title: Fix AF-0044: SFTP range number overflow bound `value > (long.MaxValue - digit) / 10` can become >= with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Ssh.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
---
# BL-1671 — Fix AF-0044: SFTP range number overflow bound `value > (long.MaxValue - digit) / 10` can become >= with no test failing

## Goal

The defect the audit office reported as AF-0044 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0044 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0044-sftp-range-number-overflow-bound-value-long-maxval.md`.

Location: `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133`

Location: `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Ssh.UnitLibrary -MaxMutants 40 -Seed 0: 'number = value > (long.MaxValue - digit) / 10 ? throw SshTransferException.SftpRangeNotDelivered() : (value * 10) + digit;' -> 'value >= ...' survived (library score 0.8421). ReadNumber ports curlx_str_number, which accepts digits up to long.MaxValue. With >=, a range number whose last step lands exactly on the bound, for example 9223372036854775807, is refused with SftpRangeNotDelivered instead of being read. That is a different exit code for an input real curl accepts. No test pins the largest accepted value or the smallest overflowing one.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133:> -Member ReadNumber -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133 (> to >=) is killed.
- Actual: survived  Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133 >

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
