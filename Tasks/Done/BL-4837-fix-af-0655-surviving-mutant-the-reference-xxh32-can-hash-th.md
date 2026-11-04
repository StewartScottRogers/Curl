---
id: BL-4837
title: Fix AF-0655: Surviving mutant: the reference XXH32 can hash the last whole 4-byte group byte by byte
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary, Curl.Conformance.Extended.UnitTests/Kafka/Compression/KafkaReferenceXxHashTests.cs]
requirement: none
created: 2026-11-04
completed: 2026-11-04
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The fix is a test only: `Hash32_InputEndingInOneWholeFourByteGroup_HashesTheGroupAsAWord` pins XXH32("abcd") = 0xA3643705 (the published vector; recomputed from xxhash_spec.md in PowerShell). "abcd" is exactly one 4-byte group, so the `<` mutant hashes it byte by byte and gives a different value, which kills it. Production code is unchanged.
- touches widened to the test file in Curl.Conformance.Extended.UnitTests (where the test has to go); no task in Doing names it.
- A lane may not run the mutation tool, so the first criterion is ticked on reasoning: the mutant changes the only path that "abcd" takes. A manual mutation run was refused by the sandbox; the re-check confirms it.
- Build clean (0 warnings); fast tests green on Windows; the test is platform-neutral.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. XXH32 of an input ending in one whole 4-byte group is pinned, killing the AF-0655 mutant
