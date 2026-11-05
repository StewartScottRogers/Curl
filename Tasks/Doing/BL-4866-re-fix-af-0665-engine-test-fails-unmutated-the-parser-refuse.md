---
id: BL-4866
title: Re-fix AF-0665: Engine test fails unmutated: the parser refuses --path-as-is as an unknown option
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-11-05
completed:
---
# BL-4866 — Re-fix AF-0665: Engine test fails unmutated: the parser refuses --path-as-is as an unknown option

## Goal

The defect the audit office reported as AF-0665 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0665 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0665-engine-test-fails-unmutated-the-parser-refuses-pat.md`.

Re-fix: the earlier task(s) BL-4853 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-05 | 2026-11-05_0819.md | reproduces: yes | Ran the reproduction: Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine still fails, 'The parser refused the command line (exit FailedInit): curl: option --path-as-is: is unknown' (TransferContextFactoryTests.cs:943).

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:186`

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:186`

The mutation baseline of Curl.Engine.UnitTests left this test out as failing unmutated (excludedTests). Run alone in a scratch worktree of HEAD it fails at TransferContextFactoryTests.cs:943: 'The parser refused the command line (exit FailedInit): curl: option --path-as-is: is unknown'. --path-as-is is in CurlHelpTable.cs:165 and CurlOptionAliasTable.cs:174, but CommandLineOptionTable.cs has no entry for it (grep finds it only in a doc comment, line 39). curl accepts --path-as-is, so a user's command line is refused.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Engine.UnitTests --filter FullyQualifiedName~Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine
```

- Expected: Passed: 1, Failed: 0
- Actual: Failed: 1 - curl: option --path-as-is: is unknown

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-05: Created.
- 2026-11-05: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
