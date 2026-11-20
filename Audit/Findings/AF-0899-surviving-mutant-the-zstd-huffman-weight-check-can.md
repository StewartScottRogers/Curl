---
id: AF-0899
title: Surviving mutant: the zstd Huffman weight check can accept trees libzstd refuses as corrupt
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:HasPairedLongestCodes-eq:surviving-mutant
reproduction: none
task: BL-5587
tasks: BL-5587
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0899 - Surviving mutant: the zstd Huffman weight check can accept trees libzstd refuses as corrupt

## Summary

High finding from the quality auditor at `Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:165`: Surviving mutant: the zstd Huffman weight check can accept trees libzstd refuses as corrupt.

## Evidence

Location: `Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:165`

Mutant weights.Count(weight => weight == 1) >= 2 -> weight != 1 survived the whole of Curl.Zstandard.UnitTests. The check is libzstd's HUF_readStats rule (two weight-1 symbols). Mutated, a description with one explicit weight 2 (completed to [2,2], no weight-1 symbol) is accepted and decoded instead of refused as corruption_detected. A valid two-symbol tree [1,1] is refused instead. Either way a --compressed zstd response's output bytes and exit code change. No test feeds a tree that this check refuses.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:165:== -Member HasPairedLongestCodes -ExcludeBaselineFailures -TimeoutSeconds 180
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-19: filed proposed.
