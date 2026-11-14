---
id: AF-0740
title: Surviving mutant: the DTLS 24-bit field limit can be raised past 24 bits; the tests derive their bound from the same constant
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Wire/DtlsHandshakeHeader.cs:MaximumUInt24-minus1:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Wire/DtlsHandshakeHeader.cs:22:-1
task: BL-5185
tasks: BL-5185
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0740 - Surviving mutant: the DTLS 24-bit field limit can be raised past 24 bits; the tests derive their bound from the same constant

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Wire/DtlsHandshakeHeader.cs:22`: Surviving mutant: the DTLS 24-bit field limit can be raised past 24 bits; the tests derive their bound from the same constant.

## Evidence

Location: `Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Wire/DtlsHandshakeHeader.cs:22`

Mutant public const int MaximumUInt24 = (1 << 24) - 1 -> (1 << 24) + 1 survived in both runs (seed 641133). Encode then accepts lengths and offsets of 2^24 and 2^24+1 and writes them truncated into 3 bytes (RFC 6347 section 4.2.2). Encode_Rfc6347Section4_2_2_RejectsFieldsOutOfRange and Split_Rfc6347Section4_2_2_RejectsABodyPast24Bits compute their out-of-range value as DtlsHandshakeHeader.MaximumUInt24 + 1, so they move with the mutated constant and cannot catch it; no test pins 16777215.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Coap.UnitLibrary/Dtls/Wire/DtlsHandshakeHeader.cs:22:-1 -Member MaximumUInt24 -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-13: filed proposed.
