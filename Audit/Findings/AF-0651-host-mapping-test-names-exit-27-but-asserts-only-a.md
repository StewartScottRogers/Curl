---
id: AF-0651
title: Host-mapping test names exit 27 but asserts only acceptance and the converted text
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:TryNormalizeReadingHost_MeasuredLetterlikeSymbolCurlRefusesWithExit27ThroughAProxy_ConvertsToSlashText:name-lies
reproduction: none
task: BL-4833
tasks: BL-4833
found: 2026-11-04
found-at: 650d20c75108547e7bc74da2e86fbee9c49f7931
scorecard: 2026-11-04_0643.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0651 - Host-mapping test names exit 27 but asserts only acceptance and the converted text

## Summary

High finding from the quality auditor at `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:580`: Host-mapping test names exit 27 but asserts only acceptance and the converted text.

## Evidence

Location: `Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs:580`

The name states exit 27 through a proxy. Its helper AssertConvertsTo asserts only Assert.IsTrue(accepted) and Assert.AreEqual(expected, idnHost). Exit 27 is never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Abstractions.UnitTests/CurlUrlHostNameMappingTests.cs -Pattern 'private static void AssertConvertsTo' -Context 0,6
```

- Expected: An assertion of exit 27, or a name without the number.
- Actual: Assert.IsTrue(accepted); Assert.AreEqual(expected, idnHost);

## Re-audits

- 2026-11-04 | 2026-11-04_1442.md | reproduces: no | Ran the Select-String: AssertConvertsTo (lines 587-593) is unchanged, but no test that calls it names exit 27 any more (grep finds no Exit27/Exits27 in Curl.Protocol.Abstractions.UnitTests). The former test is now TryNormalizeReadingHost_LetterlikeSymbolWithSlashDecomposition_IsAcceptedAndConvertsToSlashText, whose name promises only acceptance and the text the helper asserts.

## Log

- 2026-11-04: filed proposed.
- 2026-11-04: proposed -> accepted.
