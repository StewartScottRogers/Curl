---
id: AF-0822
title: Surviving mutant: libtest's default verbose trace can write text starting with LF as one prefixed line
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Conformance.LibTest.UnitLibrary/LibTestDefaultVerboseTrace.cs:WriteLines-lt:surviving-mutant
reproduction: mutation Curl.Conformance.LibTest.UnitLibrary/LibTestDefaultVerboseTrace.cs:43:<
task: none
tasks:
found: 2026-11-16
found-at: 74f3f45c47e86300b2b262dc1dacd43b508b09aa
scorecard: 2026-11-16_0514.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0822 - Surviving mutant: libtest's default verbose trace can write text starting with LF as one prefixed line

## Summary

Medium finding from the quality auditor at `Curl.Conformance.LibTest.UnitLibrary/LibTestDefaultVerboseTrace.cs:43`: Surviving mutant: libtest's default verbose trace can write text starting with LF as one prefixed line.

## Evidence

Location: `Curl.Conformance.LibTest.UnitLibrary/LibTestDefaultVerboseTrace.cs:43`

`end = end < 0 ? text.Length : end + 1` changed to `end <= 0` survived. For text whose first byte is '\n' (IndexOf gives 0), the original writes the prefix and '\n', then the prefix and the rest. The mutant writes the whole text after one prefix, changing the trace output. Trace_TextAndHeaders_PrefixesEachLineAsLibcurlsDefaultVerboseOutputDoes has no text starting with LF.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Conformance.LibTest.UnitLibrary/LibTestDefaultVerboseTrace.cs:43:< -Member WriteLines -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
