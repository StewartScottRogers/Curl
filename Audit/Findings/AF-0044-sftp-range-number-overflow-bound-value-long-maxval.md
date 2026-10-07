---
id: AF-0044
title: SFTP range number overflow bound `value > (long.MaxValue - digit) / 10` can become >= with no test failing
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:ReadNumber-GreaterToGreaterOrEqual:surviving-mutant
task: none
found: 2026-10-07
found-at: 5a627a2fb4baf7b4b2662dc309939ec576dcad20
scorecard: 2026-10-07_0844.md
closed:
closed-by:
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
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Protocol.Ssh.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-ssh.json
```

- Expected: The mutant at Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133 (> to >=) is killed.
- Actual: survived  Curl.Protocol.Ssh.UnitLibrary/Sftp/SftpDownloadPart.cs:133 >

## Re-audits

## Log

- 2026-10-07: filed proposed.
