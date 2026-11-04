---
id: BL-4817
title: Fix AF-0638: Surviving mutant: a QUIC dial whose trust anchors are unusable can go on to the next address, and no test fails
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4817 — Fix AF-0638: Surviving mutant: a QUIC dial whose trust anchors are unusable can go on to the next address, and no test fails

## Goal

The defect the audit office reported as AF-0638 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0638 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0638-surviving-mutant-a-quic-dial-whose-trust-anchors-a.md`.

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:210`

Location: `Curl.Networking.UnitLibrary/QuicDialer.cs:210`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 221035: survived, 'return (await TrustAnchorsUnusableAsync(...), false);' -> 'true'. The tuple is AttemptAsync's (Result, TryNextAddress), and line 126 acts on tryNextAddress. With unusable CA settings, the mutant retries every remaining address instead of stopping at the first refusal, which changes the -v lines and the time and attempts before exit. No test dials a name with two addresses under unusable trust anchors.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/QuicDialer.cs:210:false -Member DialThroughTunnelAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
