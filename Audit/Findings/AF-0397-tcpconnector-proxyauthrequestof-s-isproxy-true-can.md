---
id: AF-0397
title: TcpConnector.ProxyAuthRequestOf's IsProxy: true can become false with no test failing, so proxy Negotiate uses --service-name instead of --proxy-service-name
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/TcpConnector.cs:ProxyAuthRequestOf-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.cs:1516:true
task: BL-3513
tasks: BL-3444, BL-3513
found: 2026-10-23
found-at: 94da765e1b4eb040511b48ff68f7b42c5226e207
scorecard: 2026-10-23_2322.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0397 - TcpConnector.ProxyAuthRequestOf's IsProxy: true can become false with no test failing, so proxy Negotiate uses --service-name instead of --proxy-service-name

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.cs:1516`: TcpConnector.ProxyAuthRequestOf's IsProxy: true can become false with no test failing, so proxy Negotiate uses --service-name instead of --proxy-service-name. Reported by an auditor flagged unreliable in 2026-10-23_2322.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:1516`

Sampled mutant (seed 0) survived: 'IsProxy: true);' -> 'IsProxy: false);' in the HttpAuthRequest built for the CONNECT tunnel. NegotiateHttpAuthenticator.cs:152 picks the SPN service with request.IsProxy ? options.ProxyServiceName : options.ServiceName, so with the mutant a proxy Negotiate exchange asks for a ticket for the origin's service name and sends a different Proxy-Authorization token - request bytes. No test sets distinct service and proxy service names on a Negotiate tunnel.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:1516:true -Member ProxyAuthRequestOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-24 | 2026-10-24_0845.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site Curl.Networking.UnitLibrary/TcpConnector.cs:1516:true run: resolvedLine 1516, 'IsProxy: true);' -> 'IsProxy: false);', outcome survived. The same site also survived in this audit's seed-0 sample of Curl.Networking.UnitLibrary. Runner's targeted mutation rerun on the clean audited commit: survived.
- 2026-10-24 | 2026-10-24_2106.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: Curl.Networking.UnitLibrary/TcpConnector.cs:1516 ProxyAuthRequestOf 'IsProxy: true);' -> 'IsProxy: false);' resolvedLine 1516, outcome survived (5 baseline-failing TcpConnectorQuicTests excluded). The seed-0 sample hit the same site and it survived there too. Runner's targeted mutation rerun on the clean audited commit: survived.
- 2026-10-25 | 2026-10-25_0941.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site reproduction (Curl.Networking.UnitLibrary/TcpConnector.cs:1516:true, -Member ProxyAuthRequestOf): resolvedLine 1516, 'IsProxy: true);' -> 'IsProxy: false);', outcome survived. The same mutant also survived in this audit's seed-0 Networking sample. Runner's targeted mutation rerun on the clean audited commit: survived.

## Log

- 2026-10-23: filed proposed.
- 2026-10-24: proposed -> accepted.
