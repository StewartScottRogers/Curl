---
id: BL-3514
title: Fix AF-0436: ObservePeerVerification's ChainElements.Count > 0 can become >= 0 with no test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-25
completed:
---
# BL-3514 — Fix AF-0436: ObservePeerVerification's ChainElements.Count > 0 can become >= 0 with no test failing

## Goal

The defect the audit office reported as AF-0436 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0436 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0436-observepeerverification-s-chainelements-count-0-ca.md`.

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:426`

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:426`

Sampled mutant (seed 0) survived: 'var reportedChain = chain is { ChainElements.Count: > 0 }' -> '>= 0'. With the mutant, a chain that was built with no elements and no chain errors is reported as ListChainElements(chain), an empty list, in place of the certificates the server sent (peerCertificates). That list becomes PeerVerification's reported chain, which ConnectResult.PeerCertificates and the -v certificate lines are built from. No Curl.Networking.UnitTests test gives the verifier an empty chain, so a regression that drops the peer's certificates goes unnoticed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:426:> -Member ObservePeerVerification -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-25: Created.
- 2026-10-25: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
