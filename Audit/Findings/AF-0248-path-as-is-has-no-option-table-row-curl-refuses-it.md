---
id: AF-0248
title: --path-as-is has no option-table row: curl refuses it as unknown, and 13 Curl.Cli and 1 Curl.Engine unit tests fail unmutated
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:path-as-is:failing-tests
reproduction: none
task: BL-2759
tasks: BL-2759
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0248 - --path-as-is has no option-table row: curl refuses it as unknown, and 13 Curl.Cli and 1 Curl.Engine unit tests fail unmutated

## Summary

High finding from the quality auditor at `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:329`: --path-as-is has no option-table row: curl refuses it as unknown, and 13 Curl.Cli and 1 Curl.Engine unit tests fail unmutated. Reported by an auditor flagged unreliable in 2026-10-18_1045.md.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineOptionTable.cs:329`

Found in step 3: the Engine mutation baseline had to leave out Curl.Engine.TransferContextFactoryTests.Create_CommandLineOptions_AreCopiedFromTheParsedCommandLine, which fails at `Assert.IsTrue(parsed.IsAccepted)` (TransferContextFactoryTests.cs:923). A probe (a C# file-based app in the scratch folder, calling CommandLineParser.Parse with that test's arguments) printed `accepted=False refusal=FailedInit curl: option --path-as-is: is unknown`. The negatable-flag rows around line 328-330 cover tr-encoding, ignore-content-length and http0.9 but not path-as-is, although CommandLineOptions.PathAsIs (line 1950), CurlOptionAliasTable.cs:174 and CurlHelpTable.cs:163 all expect it. Real curl accepts --path-as-is, so Curl exits 2 where curl transfers. `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` gives Failed: 13, Passed: 3939, including Parse_PathAsIs_SetsPathAsIs and Parse_NoPathAsIsAfterPathAsIs_TurnsPathAsIsOff.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"
```

- Expected: Failed: 0.
- Actual: Failed!  - Failed: 13, Passed: 3939, Skipped: 18, Total: 3970 (Parse_PathAsIs_SetsPathAsIs and 12 others).

## Re-audits

- 2026-10-18 | 2026-10-18_1905.md | reproduces: no | Ran dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration": Passed, Failed 0, Passed 3952, Skipped 18. CommandLineOptionTable.cs:330 has the path-as-is NegatableFlag row.

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
