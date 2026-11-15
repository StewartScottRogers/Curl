---
id: AF-0739
title: Surviving mutant: a CoAP notification's 4-byte Max-Age can be ignored for the 60-second default
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs:MaxAgeOf-le:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs:101:<=
task: BL-5184
tasks: BL-5184
found: 2026-11-13
found-at: 01e399058f0175f566ebf05c3a643be509c45063
scorecard: 2026-11-13_2321.md
duplicate-of:
closed: 2026-11-14
closed-how: mechanical
closed-by: 2026-11-14_1335.md
---
# AF-0739 - Surviving mutant: a CoAP notification's 4-byte Max-Age can be ignored for the 60-second default

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs:101`: Surviving mutant: a CoAP notification's 4-byte Max-Age can be ignored for the 60-second default.

## Evidence

Location: `Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs:101`

Mutant { Value.Length: <= MaximumMaxAgeLength } -> < survived in both runs (seed 641133). A Max-Age option of exactly 4 bytes (RFC 7252 allows 0-4) would fall back to DefaultMaxAge (60 s), so --observe re-registers on the wrong schedule. The only test, MaxAgeOf_AValueLongerThanFourBytes_GivesTheDefault, uses 5 bytes; no test decodes a 4-byte Max-Age.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Coap.UnitLibrary/Observe/CoapObservation.cs:101:<= -Member MaxAgeOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-14 | 2026-11-14_1335.md | reproduces: no | Ran the -Site command: CoapObservation.cs:101 '<=' mutant outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-13: filed proposed.
- 2026-11-14: accepted -> closed. Re-audit 2026-11-14_1335.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
