---
id: AF-0900
title: Surviving mutant: the Huffman table build can skip the top weight's rank start
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:Build-le:surviving-mutant
reproduction: none
task: none
tasks:
found: 2026-11-19
found-at: 8469990748da6c83b0388ea33dec77d81d9255ae
scorecard: 2026-11-19_1633.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0900 - Surviving mutant: the Huffman table build can skip the top weight's rank start

## Summary

High finding from the quality auditor at `Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:180`: Surviving mutant: the Huffman table build can skip the top weight's rank start.

## Evidence

Location: `Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:180`

Mutant for (var weight = 1; weight <= maxNumberOfBits; weight++) -> weight < maxNumberOfBits survived. rankStarts[maxNumberOfBits] then keeps its count, not its start. With one symbol at the top weight the two coincide. With a one-bit tree (one explicit weight 1, completed to [1,1], maxNumberOfBits 1), Fill starts at index 2 of a 2-entry table and throws instead of decoding the literals. No test decodes a Huffman tree whose codes are all one bit long.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Zstandard.UnitLibrary/ZstandardHuffmanTable.cs:180:<= -Member Build -ExcludeBaselineFailures -TimeoutSeconds 180
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-19: filed proposed.
