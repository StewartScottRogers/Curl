---
id: AF-0845
title: Surviving mutant: a Socket.IO upgrade reply status line of two tokens can be refused with exit 8
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:CheckHead-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:88:<
task: BL-5365
tasks: BL-5365
found: 2026-11-17
found-at: 10c240a4859c7868ec16b722cde6234f80cc8fcb
scorecard: 2026-11-17_0430.md
duplicate-of:
closed: 2026-11-18
closed-how: mechanical
closed-by: 2026-11-18_0810.md
---
# AF-0845 - Surviving mutant: a Socket.IO upgrade reply status line of two tokens can be refused with exit 8

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:88`: Surviving mutant: a Socket.IO upgrade reply status line of two tokens can be refused with exit 8.

## Evidence

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:88`

statusLine.Length < 2 -> <= 2 survived. A valid status line without a reason phrase, such as 'HTTP/1.1 101', would fail with exit 8 'the WebSocket upgrade reply has no status line' instead of upgrading. No test uses a two-token status line.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:88:< -Member CheckHead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-18 | 2026-11-18_0810.md | reproduces: no | Ran the -Site command: SocketIoUpgrade.cs:88:< [CheckHead] resolvedLine 88, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-17: filed proposed.
- 2026-11-18: accepted -> closed. Re-audit 2026-11-18_0810.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
