---
id: AF-0655
title: Surviving mutant: the reference XXH32 can hash the last whole 4-byte group byte by byte
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/KafkaReferenceXxHash.cs:Hash32-le:surviving-mutant
reproduction: mutation Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/KafkaReferenceXxHash.cs:45:<=
task: BL-4837
tasks: BL-4837
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0655 - Surviving mutant: the reference XXH32 can hash the last whole 4-byte group byte by byte

## Summary

Medium finding from the quality auditor at `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/KafkaReferenceXxHash.cs:45`: Surviving mutant: the reference XXH32 can hash the last whole 4-byte group byte by byte.

## Evidence

Location: `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/KafkaReferenceXxHash.cs:45`

Mutant 'position + 4 <= input.Length' -> '<' survived. For inputs whose tail is exactly 4 bytes, the reference hash differs from XXH32, so the reference Kafka frames carry wrong checksums. No test pins a hash of such an input, so the conformance reference can drift from the spec unnoticed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/KafkaReferenceXxHash.cs:45:<= -Member Hash32 -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
