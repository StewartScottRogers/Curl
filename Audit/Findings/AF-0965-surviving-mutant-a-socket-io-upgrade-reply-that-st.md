---
id: AF-0965
title: Surviving mutant: a Socket.IO upgrade reply that starts with an empty line can be read on instead of failing with exit 8 (IndexOf(HeadEnd) < 0 -> <= 0)
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:ReadReplyAsync-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:64:<
task: BL-5736
tasks: BL-5736
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed: 2026-11-22
closed-how: mechanical
closed-by: 2026-11-22_1351.md
---
# AF-0965 - Surviving mutant: a Socket.IO upgrade reply that starts with an empty line can be read on instead of failing with exit 8 (IndexOf(HeadEnd) < 0 -> <= 0)

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:64`: Surviving mutant: a Socket.IO upgrade reply that starts with an empty line can be read on instead of failing with exit 8 (IndexOf(HeadEnd) < 0 -> <= 0).

## Evidence

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:64`

Seeded sample (seed 51850, -MaxMutants 25): the mutant at SocketIoUpgrade.cs:64 operator '<' survived. Original: 'while ((headEnd = received.WrittenSpan.IndexOf(HeadEnd)) < 0)'; mutated: '<= 0'. A reply beginning with CRLF CRLF has its head end at index 0. The original stops and CheckHead fails it at once with exit 8 'Socket.IO: the WebSocket upgrade reply has no status line' (lines 86-90). The mutant keeps reading until the server closes (exit 52 'Empty reply from server'), 65536 bytes arrive (exit 8, 'too long'), or the transfer times out. No test sends a reply that starts with an empty line. This changes the exit code and can hang.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:64:< -Member ReadReplyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test sends a reply starting with CRLF CRLF and expects exit 8 'no status line'
- Actual: outcome survived

## Re-audits

- 2026-11-22 | 2026-11-22_1351.md | reproduces: no | Ran the -Site command: 'IndexOf(HeadEnd)) < 0' -> '<= 0' at SocketIoUpgrade.cs:64 was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-22: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
