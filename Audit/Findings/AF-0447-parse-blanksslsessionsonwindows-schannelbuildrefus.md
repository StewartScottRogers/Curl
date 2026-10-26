---
id: AF-0447
title: Parse_BlankSslSessionsOnWindows_...SchannelBuildRefusesItAsUnsupported only checks that the 'blank argument' line is absent, never the refusal it names
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs:Parse_BlankSslSessionsOnWindows_IsNotRefusedAsBlankAsTheSchannelBuildRefusesItAsUnsupported:name-lies
reproduction: none
task: none
tasks:
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0447 - Parse_BlankSslSessionsOnWindows_...SchannelBuildRefusesItAsUnsupported only checks that the 'blank argument' line is absent, never the refusal it names

## Summary

Medium finding from the quality auditor at `Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs:114`: Parse_BlankSslSessionsOnWindows_...SchannelBuildRefusesItAsUnsupported only checks that the 'blank argument' line is absent, never the refusal it names. Reported by an auditor flagged unreliable in 2026-10-25_1542.md.

## Evidence

Location: `Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs:114`

The test calls Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason(SslSessions). That helper (lines 103-110) asserts only CollectionAssert.DoesNotContain(result.Refusal?.StandardErrorLines ?? [], blankLine). It passes when the parse is accepted (Refusal null) as well as when it is refused for any reason. The name promises the Schannel build refuses --ssl-sessions as unsupported, but no exit code or message is asserted.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs -Pattern 'void Parse_BlankArgument_IsNotRefusedAsBlankWhereCurlRefusesItForAnotherReason' -Context 0,7
```

- Expected: The Windows test asserts the refusal: IsAccepted false, the exit code and the unsupported-option line.
- Actual: The helper's only assertion is CollectionAssert.DoesNotContain(... blankLine).

## Re-audits

## Log

- 2026-10-25: filed proposed.
