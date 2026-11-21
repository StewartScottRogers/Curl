---
id: AF-0930
title: Surviving mutant: an empty CURL_CA_BUNDLE can be taken as the CA bundle path
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:NonEmptyCurlCaBundle-gt:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:124:>
task: BL-5689
tasks: BL-5689
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0930 - Surviving mutant: an empty CURL_CA_BUNDLE can be taken as the CA bundle path

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:124`: Surviving mutant: an empty CURL_CA_BUNDLE can be taken as the CA bundle path. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:124`

Seed 812510 sample of Curl.Networking.UnitLibrary: ReadEnvironmentVariable("CURL_CA_BUNDLE") is { Length: > 0 } mutated to { Length: >= 0 } survived. curl treats an empty CURL_CA_BUNDLE as unset; the mutant loads the empty path as the default bundle, turning a verified transfer into a certificate-load failure with another exit code. No test sets CURL_CA_BUNDLE to the empty string.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:124:> -Member NonEmptyCurlCaBundle -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
