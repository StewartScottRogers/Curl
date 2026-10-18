---
id: BL-2759
title: Fix AF-0248: --path-as-is has no option-table row: curl refuses it as unknown, and 13 Curl.Cli and 1 Curl.Engine unit tests fail unmutated
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-18
completed: 2026-10-18
---
# BL-2759 — Fix AF-0248: --path-as-is has no option-table row: curl refuses it as unknown, and 13 Curl.Cli and 1 Curl.Engine unit tests fail unmutated

## Goal

The defect the audit office reported as AF-0248 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0248 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0248-path-as-is-has-no-option-table-row-curl-refuses-it.md`.

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:329`

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:329`

Found in step 3: the Engine mutation baseline had to leave out Curl.Engine.TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine, which fails at `Assert.IsTrue(parsed.IsAccepted)` (TransferContextFactoryTests.cs:923). A probe (a C# file-based app in the scratch folder, calling CommandLineParser.Parse with that test's arguments) printed `accepted=False refusal=FailedInit curl: option --path-as-is: is unknown`. The negatable-flag rows around line 328-330 cover tr-encoding, ignore-content-length and http0.9 but not path-as-is, although CommandLineOptions.PathAsIs (line 1950), CurlOptionAliasTable.cs:174 and CurlHelpTable.cs:163 all expect it. Real curl accepts --path-as-is, so Curl exits 2 where curl transfers. `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` gives Failed: 13, Passed: 3939, including Parse_PathAsIs_SetsPathAsIs and Parse_NoPathAsIsAfterPathAsIs_TurnsPathAsIsOff.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"
```

- Expected: Failed: 0.
- Actual: Failed!  - Failed: 13, Passed: 3939, Skipped: 18, Total: 3970 (Parse_PathAsIs_SetsPathAsIs and 12 others).

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-18 (lane 5): no code change needed. `CommandLineOptionTable.cs:330` already holds `NegatableFlag("path-as-is", ...)`, added in da1eaeddb long before the audit; the audited tree lacked it, so the finding most likely saw an audit-seeder planted defect (row removed) and reported it as a product defect. `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` on this checkout: Failed: 0, Passed: 3952, Skipped: 18, Total: 3970. Full `dotnet build` clean; fast tests green. Existing tests Parse_PathAsIs_SetsPathAsIs and Parse_NoPathAsIsAfterPathAsIs_TurnsPathAsIsOff already pin the row, so no new test is needed.

## Log

- 2026-10-18: Created.
- 2026-10-18: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-10-18: Doing -> Done. --path-as-is row is present; Curl.Cli.UnitTests fast run fails 0 of 3970
