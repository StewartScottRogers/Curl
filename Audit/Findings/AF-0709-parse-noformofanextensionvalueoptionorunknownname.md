---
id: AF-0709
title: Parse_NoFormOfAnExtensionValueOptionOrUnknownName_IsAnUnknownOption checks two of its three refusals only for existence
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces.
key: quality:Curl.Cli.UnitTests/CommandLineExtensionOptionTests.cs:Parse_NoFormOfAnExtensionValueOptionOrUnknownName_IsAnUnknownOption:weak-assertion
reproduction: none
task: BL-5068
tasks: BL-5068
found: 2026-11-12
found-at: acc2af773fb64825b3b57d9470bb6f96176fb127
scorecard: 2026-11-12_2105.md
duplicate-of:
closed: 2026-11-13
closed-how: reliable-reaudit
closed-by: 2026-11-13_2321.md
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

- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Parse_NoFormOfAnExtensionValueOptionOrUnknownName_IsAnUnknownOption now asserts Assert.AreEqual(CurlExitCode.FailedInit, ...) and the exact is unknown line for valueOption, unknown and extensionsOff. The only remaining match (line 88) is Assert.IsNotNull(unknown.Refusal) in a different test, Parse_AnExtensionOptionWithExtensionsOff_RefusesItAsAnUnknownOption, where it is a guard before a CollectionAssert.

## Log

- 2026-11-12: filed proposed.
- 2026-11-13: proposed -> accepted.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces.
