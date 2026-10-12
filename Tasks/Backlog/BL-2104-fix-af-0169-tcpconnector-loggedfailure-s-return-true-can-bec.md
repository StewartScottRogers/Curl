---
id: BL-2104
title: Fix AF-0169: TcpConnector.LoggedFailure's 'return true' can become 'return false' with no test failing: a failed TLS handshake is also logged as completed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-12
completed:
---
# BL-2104 — Fix AF-0169: TcpConnector.LoggedFailure's 'return true' can become 'return false' with no test failing: a failed TLS handshake is also logged as completed

## Goal

The defect the audit office reported as AF-0169 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0169 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0169-tcpconnector-loggedfailure-s-return-true-can-becom.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2267`

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2267`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: mutant TcpConnector.cs:2267 'return true;' -> 'return false;' survived. LoggedFailure logs log.Failed(Tls, exit, message) for a handshake with no connection and tells its callers (lines 2241, 2251) not to log success. Under the mutant, both callers go on to log.HandshakeCompleted / LogReportedHandshake, so the diagnostic log records a failed handshake as completed. Tests such as TcpConnectorTests.DiagnosticLog ConnectAsync_WithAClientKeyPassPhrase_NeverLogsThePassPhrase count the Error entries, but none asserts the absence of a completion entry after a failure.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:2267:true -Member LoggedFailure -ExcludeBaselineFailures -TimeoutSeconds 120
```

- Expected: outcome killed: a failed handshake's diagnostic log is asserted to hold no handshake-completed entry
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-12: Created.
