---
id: AF-0962
title: Surviving mutant: --jsonrpc whose method name ends the value can crash with IndexOutOfRangeException (MethodEnd end < text.Length -> <=)
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.JsonRpc.UnitLibrary/JsonRpcCallList.cs:MethodEnd-lt:surviving-mutant
reproduction: mutation Curl.Extension.JsonRpc.UnitLibrary/JsonRpcCallList.cs:71:<
task: BL-5733
tasks: BL-5733
found: 2026-11-22
found-at: 9f9f37761162551e34c06d99ea4c7ebfb0168e83
scorecard: 2026-11-22_0210.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0962 - Surviving mutant: --jsonrpc whose method name ends the value can crash with IndexOutOfRangeException (MethodEnd end < text.Length -> <=)

## Summary

High finding from the quality auditor at `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcCallList.cs:71`: Surviving mutant: --jsonrpc whose method name ends the value can crash with IndexOutOfRangeException (MethodEnd end < text.Length -> <=).

## Evidence

Location: `Curl.Extension.JsonRpc.UnitLibrary/JsonRpcCallList.cs:71`

Seeded sample (seed 51850, -MaxMutants 25): the mutant at JsonRpcCallList.cs:71 operator '<' survived. Original: 'while (end < text.Length && !IsWhitespace(text[end]) && text[end] is not Separator and not (byte)'[' and not (byte)'{')'; mutated: 'end <= text.Length'. With the mutant, a method name that runs to the end of the value (curl --jsonrpc get_data URL) reads text[text.Length] and throws IndexOutOfRangeException out of JsonRpcCallList.Parse, so curl crashes instead of posting the call. No test parses a value that ends in a method name: Parse_AMethodAlone_IsOneCallWithoutParamsAndId1_Section4 uses '  get_data  ' with trailing whitespace, and the batch test ends in 'get_data;'. This changes the exit code and request bytes a user sees.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.JsonRpc.UnitLibrary/JsonRpcCallList.cs:71:< -Member MethodEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test parses a value whose last call is a bare method name
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-22: filed proposed.
