---
id: BL-4853
title: Fix AF-0665: Engine test fails unmutated: the parser refuses --path-as-is as an unknown option
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitTests]
requirement: none
created: 2026-11-04
completed: 2026-11-04
---
# BL-4853 — Fix AF-0665: Engine test fails unmutated: the parser refuses --path-as-is as an unknown option

## Goal

The defect the audit office reported as AF-0665 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0665 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0665-engine-test-fails-unmutated-the-parser-refuses-pat.md`.

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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-11-04 (lane 1): The reproduction already passes at HEAD 1aaa33459 (Passed: 1, Failed: 0), with no code change. `CommandLineOptionTable.cs:332` has held `NegatableFlag("path-as-is", ...)` since da1eaeddb, long before this audit, and the test's `Parse` helper calls the static, deterministic `CommandLineParser.Parse`, so no test order can change the result. The finding says grep found `path-as-is` "only in a doc comment, line 39", which does not match HEAD. The audit tree most likely lacked that line, as a planted (seeded) defect or an older tree. A lane may not read `Audit/`, so the re-audit should confirm which. Nothing to fix in Curl.Engine.UnitTests.
- `dotnet build` is clean (0 warnings) and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) passed in all 71 test assemblies on Windows. No test changed, so nothing new depends on the platform.

## Log

- 2026-11-04: Created.
- 2026-11-04: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-04: Doing -> Done. AF-0665's reproduction passes at HEAD: --path-as-is parses, and Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine passes
