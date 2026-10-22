---
id: AF-0344
title: OpenSslCipherSuites.Find's ignoreCase: false can become true with no test failing, so cipher names in the wrong case are accepted
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Networking.UnitLibrary/OpenSslCipherSuites.cs:Find-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/OpenSslCipherSuites.cs:156:false
task: none
tasks:
found: 2026-10-22
found-at: 608a755586679cf34031a0e5b137f01b9d7fb329
scorecard: 2026-10-22_1120.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0344 - OpenSslCipherSuites.Find's ignoreCase: false can become true with no test failing, so cipher names in the wrong case are accepted

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/OpenSslCipherSuites.cs:156`: OpenSslCipherSuites.Find's ignoreCase: false can become true with no test failing, so cipher names in the wrong case are accepted.

## Evidence

Location: `Curl.Networking.UnitLibrary/OpenSslCipherSuites.cs:156`

Sampled mutant (seed 0) survived: 'Enum.TryParse<TlsCipherSuite>(entry, ignoreCase: false, ...)' became 'ignoreCase: true'. An IANA cipher name such as TLS_aes_128_gcm_sha256 then matches a suite instead of being unknown. A --ciphers / --tls13-ciphers list the OpenSSL build refuses (exit 59) would be accepted. No test pins the case-sensitive match.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/OpenSslCipherSuites.cs:156:false -Member Find -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
