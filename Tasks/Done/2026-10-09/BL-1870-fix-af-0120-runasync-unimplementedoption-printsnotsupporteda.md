---
id: BL-1870
title: Fix AF-0120: RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer is always Inconclusive: no option is unimplemented, so it never runs
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-09
completed: 2026-10-09
---
# BL-1870 — Fix AF-0120: RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer is always Inconclusive: no option is unimplemented, so it never runs

## Goal

The defect the audit office reported as AF-0120 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0120 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0120-runasync-unimplementedoption-printsnotsupportedand.md`.

Location: `Curl.Console.UnitTests/CurlCommandRunnerUnimplementedOptionTests.cs:50`

Location: `Curl.Console.UnitTests/CurlCommandRunnerUnimplementedOptionTests.cs:50`

Line 48-51: 'if (name is null) { Assert.Inconclusive("Every curl 8.21.0 option has a row: nothing is unimplemented."); }'. FirstUnimplementedName is null on this tree, so both data rows end Inconclusive on every run and the not-supported path (exit 2, the 'installed libcurl version does not support this' text, no transfer) is never tested here.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Console.UnitTests -c Release -nologo --filter "FullyQualifiedName~RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer"
```

- Expected: Both rows pass.
- Actual: Skipped RunAsync_UnimplementedOption_PrintsNotSupportedAndExitsTwoWithoutATransfer (True) and (False); Total tests: 2, Skipped: 2.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Every curl 8.21.0 option now has a `CommandLineOptionTable` row, so the parser's generic unimplemented-option refusal (`RefuseUnlistedName`) cannot be reached from a real option name and the test always went Inconclusive. The same refusal bytes and exit 2 come from the Windows Schannel build's refusal of `--http3` - the very command line the class comment says was measured (curl 8.21.0, 2026-09-28) - so the test now runs the runner with `parsesAsWindowsBuild: true` and `--http3` (and `-s --http3`) on every platform. The test name is kept so the finding's reproduction filter still selects it; to the Schannel build `--http3` is an unimplemented option.
- Reproduction after the fix: Passed 2, Skipped 0. Build clean; fast tests green.

## Log

- 2026-10-09: Created.
- 2026-10-09: Backlog -> Doing.
- 2026-10-09: Doing -> Done. Test now drives the Schannel build's --http3 refusal; reproduction passes 2 of 2
