---
id: AF-0654
title: Surviving mutant: an empty UDP datagram over CONNECT-IP can be dropped
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.Masque.UnitLibrary/Wire/IpUdpPacket.cs:TryReadUdp-lt:surviving-mutant
reproduction: mutation Curl.Extension.Masque.UnitLibrary/Wire/IpUdpPacket.cs:148:<
task: BL-4836
tasks: BL-4836
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed: 2026-11-04
closed-how: mechanical
closed-by: 2026-11-04_1442.md
---
# AF-0654 - Surviving mutant: an empty UDP datagram over CONNECT-IP can be dropped

## Summary

Medium finding from the quality auditor at `Curl.Extension.Masque.UnitLibrary/Wire/IpUdpPacket.cs:148`: Surviving mutant: an empty UDP datagram over CONNECT-IP can be dropped.

## Evidence

Location: `Curl.Extension.Masque.UnitLibrary/Wire/IpUdpPacket.cs:148`

Mutant 'segment.Length < UdpHeaderLength' -> '<=' survived. A segment of exactly 8 bytes (a UDP header with an empty payload) then reads udpLength 0 and is refused, so the datagram is dropped. No test reads a zero-length UDP payload.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Masque.UnitLibrary/Wire/IpUdpPacket.cs:148:< -Member TryReadUdp -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-04 | 2026-11-04_1442.md | reproduces: no | Ran the -Site command: resolvedLine 148, mutated 'segment.Length <= UdpHeaderLength ? 0 : ...', outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
- 2026-11-04: accepted -> closed. Re-audit 2026-11-04_1442.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
