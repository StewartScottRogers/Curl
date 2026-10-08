---
id: AF-0044
title: SFTP range number overflow bound `value > (long.MaxValue - digit) / 10` can become >= with no test failing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:ReadNumber-gt:surviving-mutant
reproduction: mutation Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133:>
task: BL-1671
tasks: BL-1671
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
duplicate-of:
closed: 2026-10-08
closed-how: mechanical
closed-by: 2026-10-08_0748.md
---
# AF-0044 - SFTP range number overflow bound `value > (long.MaxValue - digit) / 10` can become >= with no test failing

## Summary

High finding from the quality auditor at `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133`: SFTP range number overflow bound `value > (long.MaxValue - digit) / 10` can become >= with no test failing. Reported by an auditor flagged unreliable in 2026-10-07_0844.md.

## Evidence

Location: `Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Ssh.UnitLibrary -MaxMutants 40 -Seed 0: 'number = value > (long.MaxValue - digit) / 10 ? throw SshTransferException.SftpRangeNotDelivered() : (value * 10) + digit;' -> 'value >= ...' survived (library score 0.8421). ReadNumber ports curlx_str_number, which accepts digits up to long.MaxValue. With >=, a range number whose last step lands exactly on the bound, for example 9223372036854775807, is refused with SftpRangeNotDelivered instead of being read. That is a different exit code for an input real curl accepts. No test pins the largest accepted value or the smallest overflowing one.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133:> -Member ReadNumber -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: The mutant at Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133 (> to >=) is killed.
- Actual: survived  Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133 >

## Re-audits

- 2026-10-07 | 2026-10-07_1336.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site reproduction: resolvedLine 133, outcome survived (score 0). Runner's targeted mutation rerun on the clean audited commit: survived.
- 2026-10-08 | 2026-10-08_0748.md | reproduces: no | Ran the -Site command: killed at SftpDownloadPart.cs:133 ('value > (long.MaxValue - digit) / 10' -> '>='). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-07: filed proposed.
- 2026-10-07: proposed -> accepted.
- 2026-10-08: accepted -> closed. Re-audit 2026-10-08_0748.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
