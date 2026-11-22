---
id: AF-0964
title: Surviving mutant: a Socket.IO upgrade reply head of exactly 65536 bytes can be refused as too long (WrittenCount > LongestHead -> >=)
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:ReadReplyAsync-gt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:66:>
task: BL-5735
tasks: BL-5735
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed: 2026-11-22
closed-how: mechanical
closed-by: 2026-11-22_1351.md
---
# AF-0964 - Surviving mutant: a Socket.IO upgrade reply head of exactly 65536 bytes can be refused as too long (WrittenCount > LongestHead -> >=)

## Summary

High finding from the quality auditor at `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:66`: Surviving mutant: a Socket.IO upgrade reply head of exactly 65536 bytes can be refused as too long (WrittenCount > LongestHead -> >=).

## Evidence

Location: `Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:66`

Seeded sample (seed 51850, -MaxMutants 25): the mutant at SocketIoUpgrade.cs:66 operator '>' survived. Original: 'if (received.WrittenCount > LongestHead)' with LongestHead = 65536 (line 19); mutated: '>='. With the mutant, a reply whose head end has not arrived after exactly 65536 bytes is refused with exit 8 'Socket.IO: the WebSocket upgrade reply head is too long' instead of being read on. The only limit test, ReadReplyAsync_AHeadLongerThanTheLimit_FailsWithExit8, sends 20 x 4000+ bytes, far past the limit, so the boundary is never tested. This changes the exit code.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.SocketIo.UnitLibrary/SocketIoUpgrade.cs:66:> -Member ReadReplyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test reads a head that reaches exactly the limit
- Actual: outcome survived

## Re-audits

- 2026-11-22 | 2026-11-22_1351.md | reproduces: no | Ran the -Site command: 'received.WrittenCount > LongestHead' -> '>=' at SocketIoUpgrade.cs:66 was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-22: filed proposed.
- 2026-11-22: accepted -> closed. Re-audit 2026-11-22_1351.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
