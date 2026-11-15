---
id: AF-0778
title: Surviving mutant: --dns-over-tls/--dns-over-quic can refuse a name of exactly 255 octets
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.EncryptedDns.UnitLibrary/Wire/DnsQueryEncoder.cs:EncodeName-gt:surviving-mutant
reproduction: mutation Curl.Extension.EncryptedDns.UnitLibrary/Wire/DnsQueryEncoder.cs:62:>
task: none
tasks:
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0778 - Surviving mutant: --dns-over-tls/--dns-over-quic can refuse a name of exactly 255 octets

## Summary

High finding from the quality auditor at `Curl.Extension.EncryptedDns.UnitLibrary/Wire/DnsQueryEncoder.cs:62`: Surviving mutant: --dns-over-tls/--dns-over-quic can refuse a name of exactly 255 octets.

## Evidence

Location: `Curl.Extension.EncryptedDns.UnitLibrary/Wire/DnsQueryEncoder.cs:62`

return name.Count > MaximumNameLength ? throw ... : [.. name]; with MaximumNameLength = 255; mutated to >= survived. RFC 1035 section 2.3.4 allows 255 octets. With the mutant, a maximum-length name is refused, so it resolves to nothing and the transfer fails with exit 6. Encode_Rfc1035Section2_3_4_NameOver255Octets_IsRefused uses four 63-octet labels (257 octets), so the boundary is never tested.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.EncryptedDns.UnitLibrary/Wire/DnsQueryEncoder.cs:62:> -Member EncodeName -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-15: filed proposed.
