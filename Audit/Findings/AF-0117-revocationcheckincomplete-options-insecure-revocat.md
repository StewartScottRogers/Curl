---
id: AF-0117
title: RevocationCheckIncomplete '!options.Insecure && RevocationBestEffortTolerates(chain)' can become '||' with no test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:CaCertificateFileUnusable-and:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:129:&&
task: none
tasks:
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0117 - RevocationCheckIncomplete '!options.Insecure && RevocationBestEffortTolerates(chain)' can become '||' with no test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:129`: RevocationCheckIncomplete '!options.Insecure && RevocationBestEffortTolerates(chain)' can become '||' with no test failing. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:129`

Mutant '!options.Insecure && RevocationBestEffortTolerates(chain)' -> '!options.Insecure || ...' survived (the tool names the member CaCertificateFileUnusable; the line is in Judge). With the mutant every verified (non-insecure) handshake is marked RevocationCheckIncomplete, so SslStreamTlsProvider.ReportRevocationCheckIncomplete (line 646) reports it, and TcpConnector (line 2243) writes a revocation-check-incomplete warning to the diagnostic log for every TLS connection, not only for --ssl-revoke-best-effort with offline revocation status. curl's own output is unchanged, so this is a decision rather than output bytes. No test asserts the flag is false on a normal verified handshake.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/ServerCertificateVerification.cs:129:&& -Member CaCertificateFileUnusable -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | reproduces: yes | Ran the -Site reproduction: survived ServerCertificateVerification.cs:129 && [CaCertificateFileUnusable] 'RevocationCheckIncomplete = !options.Insecure && RevocationBestEffortTolerates(chain)' -> '||'. It survived in the seed-0 sample as well.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
