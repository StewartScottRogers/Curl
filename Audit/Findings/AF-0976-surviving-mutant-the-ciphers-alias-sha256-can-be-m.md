---
id: AF-0976
title: Surviving mutant: the --ciphers alias SHA256 can be made to also select GCM suites (&& -> ||)
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/OpenSslCipherString.cs:readonly-and:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/OpenSslCipherString.cs:51:&&
task: BL-5773
tasks: BL-5773
found: 2026-11-22
found-at: fdfc7d13ff07752cdeb7bf999a56dd653660b7a2
scorecard: 2026-11-22_1351.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0976 - Surviving mutant: the --ciphers alias SHA256 can be made to also select GCM suites (&& -> ||)

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:51`: Surviving mutant: the --ciphers alias SHA256 can be made to also select GCM suites (&& -> ||).

## Evidence

Location: `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:51`

Invoke-MutationTest -Library Curl.Networking.UnitLibrary -MaxMutants 40 -Seed 891023: survived. Mutated ('SHA256', name => !IsGcm(name) && name.EndsWith("SHA256")) to ('SHA256', name => !IsGcm(name) || name.EndsWith("SHA256")). The SHA256 alias then selects every non-GCM suite and every suite ending SHA256, GCM ones included. In the OpenSSL build, --ciphers SHA256 would offer a different set of suites, changing the ClientHello bytes sent. No test of OpenSslCipherString pins the SHA256 alias's selection.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/OpenSslCipherString.cs:51:&& -Member readonly -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-22: filed proposed.
