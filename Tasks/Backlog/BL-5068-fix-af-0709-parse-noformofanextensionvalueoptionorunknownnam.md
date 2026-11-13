---
id: BL-5068
title: Fix AF-0709: Parse_NoFormOfAnExtensionValueOptionOrUnknownName_IsAnUnknownOption checks two of its three refusals only for existence
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-11-13
completed:
---
# BL-5068 — Fix AF-0709: Parse_NoFormOfAnExtensionValueOptionOrUnknownName_IsAnUnknownOption checks two of its three refusals only for existence

## Goal

The defect the audit office reported as AF-0709 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0709 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0709-parse-noformofanextensionvalueoptionorunknownname.md`.

Location: `Curl.Cli.UnitTests/CommandLineExtensionOptionTests.cs:61`

Location: `Curl.Cli.UnitTests/CommandLineExtensionOptionTests.cs:61`

Step 0 weak-assertion candidate. Lines 61-62: Assert.IsNotNull(valueOption.Refusal); Assert.IsNotNull(unknown.Refusal); are the only checks of --no-cache-dir and --no-nothing. Only the third case gets Assert.AreEqual(CurlExitCode.FailedInit, extensionsOff.Refusal.ExitCode). The name promises an unknown-option refusal (its exit code and curl's 'is unknown' text), but any refusal at all, such as a blank-argument or bad-value refusal with another exit code, would pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitTests/CommandLineExtensionOptionTests.cs -Pattern 'Assert.IsNotNull\((valueOption|unknown)\.Refusal\)'
```

- Expected: No match on lines 61-62: each refusal's exit code and message are asserted exactly.
- Actual: CommandLineExtensionOptionTests.cs:61: Assert.IsNotNull(valueOption.Refusal); and :62: Assert.IsNotNull(unknown.Refusal);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-13: Created.
