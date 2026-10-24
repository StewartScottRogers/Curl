---
id: BL-3444
title: Fix AF-0397: TcpConnector.ProxyAuthRequestOf's IsProxy: true can become false with no test failing, so proxy Negotiate uses --service-name instead of --proxy-service-name
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-24
completed:
---
# BL-3444 — Fix AF-0397: TcpConnector.ProxyAuthRequestOf's IsProxy: true can become false with no test failing, so proxy Negotiate uses --service-name instead of --proxy-service-name

## Goal

The defect the audit office reported as AF-0397 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0397 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0397-tcpconnector-proxyauthrequestof-s-isproxy-true-can.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1516`

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1516`

Sampled mutant (seed 0) survived: 'IsProxy: true);' -> 'IsProxy: false);' in the HttpAuthRequest built for the CONNECT tunnel. NegotiateHttpAuthenticator.cs:152 picks the SPN service with request.IsProxy ? options.ProxyServiceName : options.ServiceName, so with the mutant a proxy Negotiate exchange asks for a ticket for the origin's service name and sends a different Proxy-Authorization token - request bytes. No test sets distinct service and proxy service names on a Negotiate tunnel.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:1516:true -Member ProxyAuthRequestOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-24: Created.
