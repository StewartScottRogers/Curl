---
id: AF-0756
title: Unmutated Engine test fails: the parser refuses --path-as-is as an unknown option
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces.
key: quality:Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:path-as-is:failing-test
reproduction: none
task: BL-5205
tasks: BL-5205
found: 2026-11-14
found-at: fc4d1268bd4c1bce123dba90334b07e141be698a
scorecard: 2026-11-14_1335.md
duplicate-of:
closed: 2026-11-15
closed-how: reliable-reaudit
closed-by: 2026-11-15_0222.md
---
# AF-0756 - Unmutated Engine test fails: the parser refuses --path-as-is as an unknown option

## Summary

High finding from the quality auditor at `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:190`: Unmutated Engine test fails: the parser refuses --path-as-is as an unknown option.

## Evidence

Location: `Curl.Engine.UnitTests/TransferContextFactoryTests.cs:190`

Invoke-MutationTest's baseline on Curl.Engine.UnitTests excluded TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine. Run alone it fails: 'The parser refused the command line (exit FailedInit): curl: option --path-as-is: is unknown'. --path-as-is is in CurlOptionAliasTable.cs:174 and CurlHelpTable.cs:165, but CommandLineOptionTable.cs has no handler entry for it (grep finds no 'path-as-is' registration and nothing sets PathAsIs). So 'curl --path-as-is URL' exits 2 instead of sending the path unsquashed. Kind 'failing-test' as above.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Engine.UnitTests/Curl.Engine.UnitTests.csproj -c Release --filter "FullyQualifiedName~Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine"
```

- Expected: Passed.
- Actual: Failed: curl: option --path-as-is: is unknown.

## Re-audits

- 2026-11-14 | 2026-11-14_1801.md | reproduces: no | Ran the dotnet test reproduction: 'Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1' for Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine. Two other Engine tests (AwsSigV4HttpAuthenticatorTests) do fail unmutated; they are filed as a new finding.
- 2026-11-15 | 2026-11-15_0222.md | reproduces: no | Ran the dotnet test command: Passed! - Failed: 0, Passed: 1, Total: 1.

## Log

- 2026-11-14: filed proposed.
- 2026-11-15: accepted -> closed. Re-audit 2026-11-15_0222.md: the reproduction no longer reproduces.
