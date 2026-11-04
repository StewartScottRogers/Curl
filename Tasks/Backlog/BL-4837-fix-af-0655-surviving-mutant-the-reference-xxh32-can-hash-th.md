---
id: BL-4837
title: Fix AF-0655: Surviving mutant: the reference XXH32 can hash the last whole 4-byte group byte by byte
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary]
requirement: none
created: 2026-11-04
completed:
---
# BL-4837 — Fix AF-0655: Surviving mutant: the reference XXH32 can hash the last whole 4-byte group byte by byte

## Goal

The defect the audit office reported as AF-0655 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0655 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0655-surviving-mutant-the-reference-xxh32-can-hash-the.md`.

Location: `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/KafkaReferenceXxHash.cs:45`

Location: `Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/KafkaReferenceXxHash.cs:45`

Mutant 'position + 4 <= input.Length' -> '<' survived. For inputs whose tail is exactly 4 bytes, the reference hash differs from XXH32, so the reference Kafka frames carry wrong checksums. No test pins a hash of such an input, so the conformance reference can drift from the spec unnoticed.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.Extended.UnitLibrary/Kafka/Compression/KafkaReferenceXxHash.cs:45:<= -Member Hash32 -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-04: Created.
