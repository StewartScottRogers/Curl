---
id: AF-0709
title: Parse_NoFormOfAnExtensionValueOptionOrUnknownName_IsAnUnknownOption checks two of its three refusals only for existence
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Cli.UnitTests/CommandLineExtensionOptionTests.cs:Parse_NoFormOfAnExtensionValueOptionOrUnknownName_IsAnUnknownOption:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0709 - Parse_NoFormOfAnExtensionValueOptionOrUnknownName_IsAnUnknownOption checks two of its three refusals only for existence

## Summary

Medium finding from the quality auditor at `Curl.Cli.UnitTests/CommandLineExtensionOptionTests.cs:61`: Parse_NoFormOfAnExtensionValueOptionOrUnknownName_IsAnUnknownOption checks two of its three refusals only for existence. Reported by an auditor flagged unreliable in 2026-11-12_2105.md.

## Evidence

Location: `Curl.Cli.UnitTests/CommandLineExtensionOptionTests.cs:61`

Step 0 weak-assertion candidate. Lines 61-62: Assert.IsNotNull(valueOption.Refusal); Assert.IsNotNull(unknown.Refusal); are the only checks of --no-cache-dir and --no-nothing. Only the third case gets Assert.AreEqual(CurlExitCode.FailedInit, extensionsOff.Refusal.ExitCode). The name promises an unknown-option refusal (its exit code and curl's 'is unknown' text), but any refusal at all, such as a blank-argument or bad-value refusal with another exit code, would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitTests/CommandLineExtensionOptionTests.cs -Pattern 'Assert.IsNotNull\((valueOption|unknown)\.Refusal\)'
```

- Expected: No match on lines 61-62: each refusal's exit code and message are asserted exactly.
- Actual: CommandLineExtensionOptionTests.cs:61: Assert.IsNotNull(valueOption.Refusal); and :62: Assert.IsNotNull(unknown.Refusal);

## Re-audits

## Log

- 2026-11-12: filed proposed.
