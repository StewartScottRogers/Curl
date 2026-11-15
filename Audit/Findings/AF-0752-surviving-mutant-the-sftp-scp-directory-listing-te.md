---
id: AF-0752
title: Surviving mutant: the sftp/scp directory-listing test that picks the -v closing line can be inverted
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-14_1801.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:ListsDirectory-ne:surviving-mutant
reproduction: mutation Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:370:!=
task: BL-5201
tasks: BL-5201
found: 2026-11-14
found-at: fc4d1268bd4c1bce123dba90334b07e141be698a
scorecard: 2026-11-14_1335.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1801.md
---
# AF-0752 - Surviving mutant: the sftp/scp directory-listing test that picks the -v closing line can be inverted

## Summary

High finding from the quality auditor at `Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:370`: Surviving mutant: the sftp/scp directory-listing test that picks the -v closing line can be inverted.

## Evidence

Location: `Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:370`

Mutant 'context.Url.Scheme != ScpScheme && ...' -> 'context.Url.Scheme == ScpScheme && ...' survived. ListsDirectory decides, after a failed transfer, between the 'Closing connection' and 'Connection #n left intact' info lines (line 310) and feeds FailedWhileTransferring's SSH-layer case. Inverting it changes -v output bytes for a failed sftp directory listing and scp of a directory path. No test drives a failing sftp listing through the handler.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Original.Protocol.Ssh.UnitLibrary/SshProtocolHandler.cs:370:!= -Member ListsDirectory -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-14 | 2026-11-14_1801.md | reproduces: no | Ran the -Site command: SshProtocolHandler.cs:370 'context.Url.Scheme != ScpScheme' -> '==', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-14: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1801.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
