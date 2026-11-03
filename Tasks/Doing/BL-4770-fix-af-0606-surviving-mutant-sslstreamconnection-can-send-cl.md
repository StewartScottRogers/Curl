---
id: BL-4770
title: Fix AF-0606: Surviving mutant: SslStreamConnection can send close_notify more than once without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4770 — Fix AF-0606: Surviving mutant: SslStreamConnection can send close_notify more than once without a test failing

## Goal

The defect the audit office reported as AF-0606 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0606 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0606-surviving-mutant-sslstreamconnection-can-send-clos.md`.

Location: `Curl.Networking.UnitLibrary/SslStreamConnection.cs:202`

Location: `Curl.Networking.UnitLibrary/SslStreamConnection.cs:202`

_tlsEnded = true -> false survived (sample and targeted rerun, with the 102 baseline-failing tests left out): a later close or clear then shuts TLS down again, a second alert on the wire.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/SslStreamConnection.cs:202:true -Member SendCloseNotifyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
