---
id: AF-0906
title: Surviving mutant: a chain relying on an intermediate the server did not send can be observed as verified
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:PeerVerification-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:300:false
task: BL-5621
tasks: BL-5621
found: 2026-11-20
found-at: 1ce730765b795e99d4d034faf03432b41b5ec6a4
scorecard: 2026-11-20_0456.md
duplicate-of:
closed: 2026-11-20
closed-how: mechanical
closed-by: 2026-11-20_1740.md
---
# AF-0906 - Surviving mutant: a chain relying on an intermediate the server did not send can be observed as verified

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:300`: Surviving mutant: a chain relying on an intermediate the server did not send can be observed as verified.

## Evidence

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:300`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 912464: 'new PeerVerification(false, OpenSslVerifyResult.UnableToGetIssuerCertificateLocally, peerCertificates)' mutated to true survived. Judge returns this observation with the PeerFailedVerification refusal when RelyingOnAnIntermediateTheServerDidNotSend. Its Verified flag becomes the handshake event's CertificateVerified (SslStreamTlsProvider.cs:634/696), which NetworkDiagnosticLog.cs:294 writes as 'verified' or 'not verified'. With the mutant, a refused certificate is logged as verified, and no test notices. The refusal and exit 60 are unchanged, so this is a recorded decision, not exit-code output.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:300:false -Member PeerVerification -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-20 | 2026-11-20_1740.md | reproduces: no | Ran the -Site command on df0e14366f: ServerCertificateVerification.cs:300:false (PeerVerification), resolvedLine 300, outcome killed (17.3 s). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-20: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
