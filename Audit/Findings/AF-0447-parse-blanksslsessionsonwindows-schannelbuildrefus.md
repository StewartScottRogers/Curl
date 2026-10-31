---
id: AF-0447
title: Parse_BlankSslSessionsOnWindows_...SchannelBuildRefusesItAsUnsupported only checks that the 'blank argument' line is absent, never the refusal it names
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-29_0606.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1944.md, 2026-10-29_0606.md).
key: quality:Curl.Cli.UnitTests/CommandLineBlankArgumentTests.cs:Parse_BlankSslSessionsOnWindows_IsNotRefusedAsBlankAsTheSchannelBuildRefusesItAsUnsupported:name-lies
reproduction: none
task: BL-3524
tasks: BL-3524
found: 2026-10-25
found-at: cb96798969fdc083bbbf245bc653f5fc69cb8acf
scorecard: 2026-10-25_1542.md
duplicate-of:
closed: 2026-10-29
closed-how: consecutive
closed-by: 2026-10-25_1944.md, 2026-10-29_0606.md
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

- 2026-10-25 | 2026-10-25_1944.md | reproduces: no | Parse_BlankSslSessionsOnWindows_IsNotRefusedAsBlankAsTheSchannelBuildRefusesItAsUnsupported (CommandLineBlankArgumentTests.cs:115-124) has its own body now. It asserts exit code FailedInit and CollectionAssert.AreEqual on both stderr lines: 'curl: option --ssl-sessions: the installed libcurl version does not support this' and the try-help line. The shared test at line 103 also asserts IsFalse(IsAccepted) and non-empty stderr.
- 2026-10-29 | 2026-10-29_0606.md | reproduces: no | Ran the Select-String reproduction and read the Windows test it concerns. Parse_BlankSslSessionsOnWindows_IsNotRefusedAsBlankAsTheSchannelBuildRefusesItAsUnsupported (CommandLineBlankArgumentTests.cs:118) now asserts the refusal it names: CollectionAssert.AreEqual of `curl: option --ssl-sessions: the installed libcurl version does not support this` and the try-help line, plus `Assert.AreEqual(CurlExitCode.FailedInit, result.Refusal.ExitCode)`.

## Log

- 2026-10-25: filed proposed.
- 2026-10-25: proposed -> accepted.
- 2026-10-29: accepted -> closed. Re-audit 2026-10-29_0606.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-25_1944.md, 2026-10-29_0606.md).
