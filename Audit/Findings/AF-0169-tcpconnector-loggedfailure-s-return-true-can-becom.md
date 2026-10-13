---
id: AF-0169
title: TcpConnector.LoggedFailure's 'return true' can become 'return false' with no test failing: a failed TLS handshake is also logged as completed
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-13_0925.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-12_1653.md, 2026-10-13_0925.md).
key: quality:Curl.Networking.UnitLibrary/TcpConnector.cs:LoggedFailure-false:surviving-mutant
reproduction: none
task: BL-2104
tasks: BL-2104
found: 2026-10-11
found-at: d8245edb7b661211c6628e9062e9939934939fb2
scorecard: 2026-10-11_1638.md
duplicate-of:
closed: 2026-10-13
closed-how: consecutive
closed-by: 2026-10-12_1653.md, 2026-10-13_0925.md
---
# AF-0169 - TcpConnector.LoggedFailure's 'return true' can become 'return false' with no test failing: a failed TLS handshake is also logged as completed

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.cs:2267`: TcpConnector.LoggedFailure's 'return true' can become 'return false' with no test failing: a failed TLS handshake is also logged as completed.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2267`

Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: mutant TcpConnector.cs:2267 'return true;' -> 'return false;' survived. LoggedFailure logs log.Failed(Tls, exit, message) for a handshake with no connection and tells its callers (lines 2241, 2251) not to log success. Under the mutant, both callers go on to log.HandshakeCompleted / LogReportedHandshake, so the diagnostic log records a failed handshake as completed. Tests such as TcpConnectorTests.DiagnosticLog ConnectAsync_WithAClientKeyPassPhrase_NeverLogsThePassPhrase count the Error entries, but none asserts the absence of a completion entry after a failure.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:2267:true -Member LoggedFailure -ExcludeBaselineFailures -TimeoutSeconds 120
```

- Expected: outcome killed: a failed handshake's diagnostic log is asserted to hold no handshake-completed entry
- Actual: outcome survived

## Re-audits

- 2026-10-12 | 2026-10-12_1653.md | reproduces: no | Ran Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:2267:true -Member LoggedFailure -ExcludeBaselineFailures -TimeoutSeconds 120: resolvedLine 2267, 'return true;' -> 'return false;', outcome killed (6021 ms), no excluded tests.
- 2026-10-13 | 2026-10-13_0925.md | reproduces: no | Ran the -Site reproduction: TcpConnector.cs:2267 'return true;' -> 'return false;' (LoggedFailure) resolved at line 2267, outcome killed. The baseline excluded 3 unrelated ECH tests that fail unmutated (filed separately).

## Log

- 2026-10-11: filed proposed.
- 2026-10-11: proposed -> accepted.
- 2026-10-13: accepted -> closed. Re-audit 2026-10-13_0925.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-12_1653.md, 2026-10-13_0925.md).
