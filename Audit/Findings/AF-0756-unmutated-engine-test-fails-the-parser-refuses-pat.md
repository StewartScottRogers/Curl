---
id: AF-0756
title: Unmutated Engine test fails: the parser refuses --path-as-is as an unknown option
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:path-as-is:failing-test
reproduction: none
task: BL-5205
tasks: BL-5205
found: 2026-11-14
found-at: fc4d1268bd4c1bce123dba90334b07e141be698a
scorecard: 2026-11-14_1335.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-14: filed proposed.
