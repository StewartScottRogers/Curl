---
id: BL-4836
title: Fix AF-0654: Surviving mutant: an empty UDP datagram over CONNECT-IP can be dropped
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.Masque.UnitLibrary, Curl.Extension.Masque.UnitTests/IpUdpPacketTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `TryRead_AnEmptyDatagram_GivesBackAnEmptyPayload` (IPv4 and IPv6) to `Curl.Extension.Masque.UnitTests/IpUdpPacketTests.cs`; touches widened to that file because the fix is a test in the library's own test project, and no other Doing task names it. No production code changed: the code was right, only the test was missing.
- Lanes may not run `Audit/Tools/Invoke-MutationTest.ps1`, so I applied the mutant (`<` to `<=` at IpUdpPacket.cs:148) by hand: both new test rows failed against it, and all 128 Masque tests passed once it was reverted. The quality auditor's re-audit confirms the reproduction.
- Measure-CodeQuality.ps1 not run: no production code changed.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. Empty-UDP-datagram test kills the AF-0654 mutant
