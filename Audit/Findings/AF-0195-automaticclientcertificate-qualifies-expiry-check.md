---
id: AF-0195
title: AutomaticClientCertificate.Qualifies' expiry check can become exclusive with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/AutomaticClientCertificate.cs:Qualifies-le:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/AutomaticClientCertificate.cs:54:<=
task: BL-2188
tasks: BL-2188
found: 2026-10-13
found-at: c192c3e80e58636be1d282d241a7781f7df86695
scorecard: 2026-10-13_1822.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0195 - AutomaticClientCertificate.Qualifies' expiry check can become exclusive with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/AutomaticClientCertificate.cs:54`: AutomaticClientCertificate.Qualifies' expiry check can become exclusive with no test failing.

## Evidence

Location: `Curl.Networking.UnitLibrary/AutomaticClientCertificate.cs:54`

Step 3, Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0: '&& now.UtcDateTime <= certificate.NotAfter.ToUniversalTime()' mutated to '<' survived. Qualifies decides which store certificate --ssl-auto-client-cert presents. The doc comment says the time must lie within the certificate's validity, inclusive. AutomaticClientCertificateTests only tests times days away from NotAfter (Now.AddDays(daysFromNow)), so nothing pins the inclusive end. At its last valid instant a certificate would stop being offered and the handshake would go out without a client certificate.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/AutomaticClientCertificate.cs:54:<= -Member Qualifies -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-13: filed proposed.
- 2026-10-13: proposed -> accepted.
