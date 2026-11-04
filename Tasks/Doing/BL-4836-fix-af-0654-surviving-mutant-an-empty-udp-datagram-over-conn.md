---
id: BL-4836
title: Fix AF-0654: Surviving mutant: an empty UDP datagram over CONNECT-IP can be dropped
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Masque.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4836 — Fix AF-0654: Surviving mutant: an empty UDP datagram over CONNECT-IP can be dropped

## Goal

The defect the audit office reported as AF-0654 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0654 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0654-surviving-mutant-an-empty-udp-datagram-over-connec.md`.

Location: `Curl.Extension.Masque.UnitLibrary/Wire/IpUdpPacket.cs:148`

Location: `Curl.Extension.Masque.UnitLibrary/Wire/IpUdpPacket.cs:148`

Mutant 'segment.Length < UdpHeaderLength' -> '<=' survived. A segment of exactly 8 bytes (a UDP header with an empty payload) then reads udpLength 0 and is refused, so the datagram is dropped. No test reads a zero-length UDP payload.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.Masque.UnitLibrary/Wire/IpUdpPacket.cs:148:< -Member TryReadUdp -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
