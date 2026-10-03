---
id: AF-0031
title: Certificate name lookup `forIssuer: false` can become true with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/TlsFailureMessages.cs:CertificateSubjectName:surviving-mutant
task: none
found: 2026-10-03
found-at: d1db9881553d55cd92c0e74bb41561c3dea9ea84
scorecard: 2026-10-03_1233.md
closed:
closed-by:
---
# AF-0031 - Certificate name lookup `forIssuer: false` can become true with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320`: Certificate name lookup `forIssuer: false` can become true with no test failing. Reported by an auditor flagged unreliable in 2026-10-03_1233.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320`

Mutation seed 0 changed `certificate.GetNameInfo(X509NameType.SimpleName, forIssuer: false)` to `forIssuer: true`; survived. The error text would name the issuer instead of the subject, and no test notices.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 0 -TimeoutSeconds 300 -OutFile $env:TEMP\mutation-net.json
```

- Expected: Mutant at TlsFailureMessages.cs:320 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320 false

## Re-audits

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
