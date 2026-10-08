---
id: AF-0031
title: Certificate name lookup `forIssuer: false` can become true with no test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/TlsFailureMessages.cs:OpenSslPeerFailedVerification-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320:false
task: BL-1608
tasks: BL-1373, BL-1380, BL-1608
found: 2026-10-03
found-at: d1db9881553d55cd92c0e74bb41561c3dea9ea84
scorecard: 2026-10-03_1233.md
duplicate-of:
closed: 2026-10-07
closed-how: mechanical
closed-by: 2026-10-07_1336.md
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
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320:false -Member OpenSslPeerFailedVerification -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: Mutant at TlsFailureMessages.cs:320 is killed.
- Actual: survived  Curl.Networking.UnitLibrary/TlsFailureMessages.cs:320 false

## Re-audits

- 2026-10-03 | 2026-10-03_1459.md | reproduces: yes | Seed-0 mutation run: TlsFailureMessages.cs:320 `forIssuer: false` to true survived.
- 2026-10-07 | 2026-10-07_0844.md | reproduces: yes | Two sites match the description. By hand, same method as AF-0006: TlsFailureMessages.cs:320 'forIssuer: false' -> 'true' is killed by OpenSslPeerFailedVerification_WithANameMismatchOnAnIssuedCertificate_NamesTheSubjectNotTheIssuer. SchannelCommonNameCheck.cs:30 'certificate.GetNameInfo(X509NameType.DnsName, forIssuer: false)' -> 'forIssuer: true' survived ('Passed! - Failed: 0, Passed: 2996'). Every SchannelCommonNameCheckTests certificate is made by CreateSelfSigned, so issuer and subject are the same and the mutant cannot be seen. A CA-issued certificate would be matched against its issuer's name, so the --cacert accept or refuse decision (exit 60) goes untested.
- 2026-10-07 | 2026-10-07_1336.md | reproduces: no | Ran the -Site reproduction: resolvedLine 320, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
- 2026-10-07: accepted -> closed. Re-audit 2026-10-07_1336.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
