---
id: AF-0620
title: CreateTcpConnector_NoMaskWithBracketedConnectTo_DialsTheMappedAddress never checks the mapped address, only port 80, which the unmapped URL also dials
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitTests/EngineCompositionConnectToFeatureMaskTests.cs:CreateTcpConnector_NoMaskWithBracketedConnectTo_DialsTheMappedAddress:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0620 - CreateTcpConnector_NoMaskWithBracketedConnectTo_DialsTheMappedAddress never checks the mapped address, only port 80, which the unmapped URL also dials

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitTests/EngineCompositionConnectToFeatureMaskTests.cs:41`: CreateTcpConnector_NoMaskWithBracketedConnectTo_DialsTheMappedAddress never checks the mapped address, only port 80, which the unmapped URL also dials. Reported by an auditor flagged unreliable in 2026-11-03_1615.md.

## Evidence

Location: `Curl.Engine.UnitTests/EngineCompositionConnectToFeatureMaskTests.cs:41`

The mapping is 'localhost:80:[::1]:80'. The test asserts Assert.IsNotNull(result.Connection), Assert.HasCount(1, server.Targets) and Assert.AreEqual(80, server.Targets[0].Port). The URL's own port is also 80, so a connector that ignored the --connect-to mapping and dialled localhost:80 would pass. The address the name promises ([::1]) is never compared with server.Targets[0].Host.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/EngineCompositionConnectToFeatureMaskTests.cs -Pattern 'DialsTheMappedAddress' -Context 0,12
```

- Expected: The body asserts server.Targets[0].Host is the mapped ::1 address.
- Actual: Only Assert.AreEqual(80, server.Targets[0].Port) after IsNotNull and HasCount(1).

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
