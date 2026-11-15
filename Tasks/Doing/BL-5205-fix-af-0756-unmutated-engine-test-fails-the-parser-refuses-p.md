---
id: BL-5205
title: Fix AF-0756: Unmutated Engine test fails: the parser refuses --path-as-is as an unknown option
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-11-14
completed:
---
# BL-5205 — Fix AF-0756: Unmutated Engine test fails: the parser refuses --path-as-is as an unknown option

## Goal

The defect the audit office reported as AF-0756 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0756 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:190`

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:190`

Invoke-MutationTest's baseline on Curl.Engine.UnitTests excluded TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine. Run alone it fails: 'The parser refused the command line (exit FailedInit): curl: option --path-as-is: is unknown'. --path-as-is is in CurlOptionAliasTable.cs:174 and CurlHelpTable.cs:165, but CommandLineOptionTable.cs has no handler entry for it (grep finds no 'path-as-is' registration and nothing sets PathAsIs). So 'curl --path-as-is URL' exits 2 instead of sending the path unsquashed. Kind 'failing-test' as above.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Engine.UnitTests/Curl.Engine.UnitTests.csproj -c Release --filter "FullyQualifiedName~Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine"
```

- Expected: Passed.
- Actual: Failed: curl: option --path-as-is: is unknown.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
