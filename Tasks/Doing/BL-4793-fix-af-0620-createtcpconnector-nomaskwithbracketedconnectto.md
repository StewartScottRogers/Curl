---
id: BL-4793
title: Fix AF-0620: CreateTcpConnector_NoMaskWithBracketedConnectTo_DialsTheMappedAddress never checks the mapped address, only port 80, which the unmapped URL also dials
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4793 — Fix AF-0620: CreateTcpConnector_NoMaskWithBracketedConnectTo_DialsTheMappedAddress never checks the mapped address, only port 80, which the unmapped URL also dials

## Goal

The defect the audit office reported as AF-0620 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0620 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0620-createtcpconnector-nomaskwithbracketedconnectto-di.md`.

Location: `Curl.Engine.UnitTests/EngineCompositionConnectToFeatureMaskTests.cs:41`

Location: `Curl.Engine.UnitTests/EngineCompositionConnectToFeatureMaskTests.cs:41`

The mapping is 'localhost:80:[::1]:80'. The test asserts Assert.IsNotNull(result.Connection), Assert.HasCount(1, server.Targets) and Assert.AreEqual(80, server.Targets[0].Port). The URL's own port is also 80, so a connector that ignored the --connect-to mapping and dialled localhost:80 would pass. The address the name promises ([::1]) is never compared with server.Targets[0].Host.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/EngineCompositionConnectToFeatureMaskTests.cs -Pattern 'DialsTheMappedAddress' -Context 0,12
```

- Expected: The body asserts server.Targets[0].Host is the mapped ::1 address.
- Actual: Only Assert.AreEqual(80, server.Targets[0].Port) after IsNotNull and HasCount(1).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
