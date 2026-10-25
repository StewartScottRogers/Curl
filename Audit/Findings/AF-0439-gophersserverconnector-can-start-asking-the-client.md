---
id: AF-0439
title: GophersServerConnector can start asking the client for a certificate with no test failing
auditor: quality
severity: Medium
status: accepted
reason: Stewart accepted it on 2026-10-25.
key: quality:Curl.Conformance.UnitLibrary/GophersServerConnector.cs:options-false:surviving-mutant
reproduction: mutation Curl.Conformance.UnitLibrary/GophersServerConnector.cs:22:false
task: BL-3517
tasks: BL-3517
found: 2026-10-25
found-at: 03fe150efe7a6f73cd1a1c8fbd2c7bda2248009b
scorecard: 2026-10-25_0941.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0439 - GophersServerConnector can start asking the client for a certificate with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Conformance.UnitLibrary/GophersServerConnector.cs:22`: GophersServerConnector can start asking the client for a certificate with no test failing. Reported by an auditor flagged unreliable in 2026-10-25_0941.md.

## Evidence

Location: `Curl.Conformance.UnitLibrary/GophersServerConnector.cs:22`

Sampled mutant (seed 0) survived: 'new(certificate, [], false)' -> 'new(certificate, [], true)'. That third argument is TlsServerOptions.RequestClientCertificate. The class doc says the stand-in behaves as stunnel at verify = 0, which asks for no client certificate. With the mutant every gophers handshake sends a CertificateRequest, which changes the handshake curl sees (and its -v TLS lines) for gophers cases such as 1272. GophersServerConnectorTests never checks that no certificate is asked for.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.UnitLibrary/GophersServerConnector.cs:22:false -Member options -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
