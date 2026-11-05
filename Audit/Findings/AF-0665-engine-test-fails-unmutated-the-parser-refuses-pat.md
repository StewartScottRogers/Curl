---
id: AF-0665
title: Engine test fails unmutated: the parser refuses --path-as-is as an unknown option
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Engine.UnitTests/TransferContextFactoryTests.cs:Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine:failing-test
reproduction: none
task: none
tasks:
found: 2026-11-04
found-at: 4749d66adb6a165fcd90aec741b5f49017616c28
scorecard: 2026-11-04_1442.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0665 - Engine test fails unmutated: the parser refuses --path-as-is as an unknown option

## Summary

High finding from the quality auditor at `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:186`: Engine test fails unmutated: the parser refuses --path-as-is as an unknown option. Reappeared; previously AF-0366. Reported by an auditor flagged unreliable in 2026-11-04_1442.md.

## Evidence

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:186`

The mutation baseline of Curl.Engine.UnitTests left this test out as failing unmutated (excludedTests). Run alone in a scratch worktree of HEAD it fails at TransferContextFactoryTests.cs:943: 'The parser refused the command line (exit FailedInit): curl: option --path-as-is: is unknown'. --path-as-is is in CurlHelpTable.cs:165 and CurlOptionAliasTable.cs:174, but CommandLineOptionTable.cs has no entry for it (grep finds it only in a doc comment, line 39). curl accepts --path-as-is, so a user's command line is refused.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Engine.UnitTests --filter FullyQualifiedName~Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine
```

- Expected: Passed: 1, Failed: 0
- Actual: Failed: 1 - curl: option --path-as-is: is unknown

## Re-audits

## Log

- 2026-11-04: filed proposed.
