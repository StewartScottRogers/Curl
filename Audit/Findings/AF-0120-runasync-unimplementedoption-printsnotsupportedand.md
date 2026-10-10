---
id: AF-0120
title: RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer is always Inconclusive: no option is unimplemented, so it never runs
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-09_1435.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0647.md, 2026-10-09_1435.md).
key: quality:Curl.Console.UnitTests/CurlCommandRunnerUnimplementedOptionTests.cs:RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer:ignored-test
reproduction: none
task: BL-1870
tasks: BL-1870
found: 2026-10-08
found-at: cddb276d1d10fbb372f36a32cc1f588fd84c58e8
scorecard: 2026-10-08_2315.md
duplicate-of:
closed: 2026-10-09
closed-how: consecutive
closed-by: 2026-10-09_0647.md, 2026-10-09_1435.md
---
# AF-0120 - RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer is always Inconclusive: no option is unimplemented, so it never runs

## Summary

Low finding from the quality auditor at `Curl.Console.UnitTests/CurlCommandRunnerUnimplementedOptionTests.cs:50`: RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer is always Inconclusive: no option is unimplemented, so it never runs. Reported by an auditor flagged unreliable in 2026-10-08_2315.md.

## Evidence

Location: `Curl.Console.UnitTests/CurlCommandRunnerUnimplementedOptionTests.cs:50`

Line 48-51: 'if (name is null) { Assert.Inconclusive("Every curl 8.21.0 option has a row: nothing is unimplemented."); }'. FirstUnimplementedName is null on this tree, so both data rows end Inconclusive on every run and the not-supported path (exit 2, the 'installed libcurl version does not support this' text, no transfer) is never tested here.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Console.UnitTests -c Release -nologo --filter "FullyQualifiedName~RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer"
```

- Expected: Both rows pass.
- Actual: Skipped RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer (True) and (False); Total tests: 2, Skipped: 2.

## Re-audits

- 2026-10-09 | 2026-10-09_0225.md | not re-audited | Ran the dotnet test reproduction: Passed 2, Skipped 0, so the test was not Inconclusive. The detailed log shows 'ARRANGE first unimplemented option: path-as-is': it ran only because --path-as-is has no row in Curl.Cli.UnitLibrary/CommandLineOptionTable.cs (CurlOptionAliasTable.cs:173 lists it), and curl now refuses --path-as-is with exit 2. This is a product defect, not a fix: RunAsync_PathAsIs_SendsTheDotSegmentsUnsquashed fails on this tree. The test still has its 'if (name is null) Assert.Inconclusive(...)' guard (CurlCommandRunnerUnimplementedOptionTests.cs:48-51), so on a tree where every option is implemented it would be Inconclusive again. The result does not tell.
- 2026-10-09 | 2026-10-09_0647.md | reproduces: no | Ran the dotnet test reproduction: 'Passed! - Failed: 0, Passed: 2, Skipped: 0, Total: 2'. Neither data row is Inconclusive or skipped. The test now runs --<OptionTheSchannelBuildLacks> and asserts exit 2 and curl's 'the installed libcurl version does not support this' stderr.
- 2026-10-09 | 2026-10-09_1435.md | reproduces: no | Ran the reproduction: Passed 2, Failed 0, Skipped 0. Both rows of RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer run and pass (exit code 2 and the stderr text are asserted); neither is inconclusive.

## Log

- 2026-10-08: filed proposed.
- 2026-10-09: proposed -> accepted.
- 2026-10-09: accepted -> closed. Re-audit 2026-10-09_1435.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-10-09_0647.md, 2026-10-09_1435.md).
