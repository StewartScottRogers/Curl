---
id: AF-0753
title: Surviving mutant: an SFTP -Q command starting with a space can be refused as missing its parameter
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-14_1801.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Original.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs:ParseWithArguments-ge:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs:99:>=
task: BL-5202
tasks: BL-5202
found: 2026-11-14
found-at: fc4d1268bd4c1bce123dba90334b07e141be698a
scorecard: 2026-11-14_1335.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1801.md
---
# AF-0753 - Surviving mutant: an SFTP -Q command starting with a space can be refused as missing its parameter

## Summary

Medium finding from the quality auditor at `Curl.Original.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs:99`: Surviving mutant: an SFTP -Q command starting with a space can be refused as missing its parameter.

## Evidence

Location: `Curl.Original.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs:99`

Mutant 'ArgumentReader arguments = space >= 0' -> 'space > 0' survived. A quote command whose first space is at index 0 (e.g. -Q ' rm x', not trimmed by Parse) then fails with 'Syntax error command ..., missing parameter' instead of reading its arguments as curl does. The stderr text changes; no Parse_Malformed DataRow starts with a space.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Ssh.UnitLibrary/Sftp/SftpQuoteCommand.cs:99:>= -Member ParseWithArguments -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-14 | 2026-11-14_1801.md | reproduces: no | Ran the -Site command: SftpQuoteCommand.cs:99 'space >= 0' -> 'space > 0', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-14: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1801.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
