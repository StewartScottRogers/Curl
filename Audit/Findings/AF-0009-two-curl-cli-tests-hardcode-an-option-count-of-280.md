---
id: AF-0009
title: Two Curl.Cli tests hardcode an option count of 280 and fail against the 281 options in the table; the red baseline blocks mutation testing of Curl.Cli
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Cli.UnitTests/CommandLineNextGroupTests.cs:OptionTable_EveryRow_IsClassifiedAsGlobalOrPerGroupExactlyOnce:name-lies
reproduction: none
task: BL-1265
tasks: BL-1265
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0009 - Two Curl.Cli tests hardcode an option count of 280 and fail against the 281 options in the table; the red baseline blocks mutation testing of Curl.Cli

## Summary

Medium finding from the quality auditor at `Curl.Cli.UnitTests/CommandLineNextGroupTests.cs:61`: Two Curl.Cli tests hardcode an option count of 280 and fail against the 281 options in the table; the red baseline blocks mutation testing of Curl.Cli. Reported by an auditor flagged unreliable in 2026-10-02_1400.md.

## Evidence

Location: `Curl.Cli.UnitTests/CommandLineNextGroupTests.cs:61`

dotnet test Curl.Cli.UnitTests -c Release --filter TestCategory!=Integration: Failed 2, Passed 3632. `Assert.HasCount(280, CommandLineOptionTable.GlobalOptionLongNames.Concat(perGroup))` reports expected 280, actual 281. LibcurlSourceCodeOptionCoverageTests.EveryListedOption_IsParsedAndListedOnce:73 fails the same way (`Assert.HasCount(280, listed)`). Neither test says which option is the 281st, so one of them counts a magic number rather than the 'exactly once' its name promises. The mutation tool's baseline also fails, so Curl.Cli has no mutation score in this audit.

## Reproduction

Run from the repository root:

```powershell
dotnet test Curl.Cli.UnitTests -c Release --filter "Name=OptionTable_EveryRow_IsClassifiedAsGlobalOrPerGroupExactlyOnce|Name=EveryListedOption_IsParsedAndListedOnce" -nologo
```

- Expected: Passed: 2.
- Actual: Failed: 2, expected count: 280, actual count: 281.

## Re-audits

- 2026-10-03 | 2026-10-03_0623.md | reproduces: no | Ran the dotnet test reproduction: Passed! Failed 0, Passed 2, Total 2. Both tests pass.
- 2026-10-03 | 2026-10-03_1233.md | reproduces: no | dotnet test Curl.Cli.UnitTests with the two filtered tests: Passed 2, Failed 0.
- 2026-10-03 | 2026-10-03_1459.md | reproduces: no | Both named Curl.Cli tests pass (Passed: 2, Failed: 0).
- 2026-10-07 | 2026-10-07_0844.md | not re-audited | overlaps planted defect PD-303 in Curl.Cli.UnitLibrary/CommandLineOptionTable.cs (the --path-as-is row removed: 282 options counted against the test's 281), so the auditor's verdict (reproduces yes) is set aside and is not a reproduction (corrected 2026-10-07, ADR-0422): Ran the reproduction: 'Failed! - Failed: 2, Passed: 0'. Both OptionTable_EveryRow_IsClassifiedAsGlobalOrPerGroupExactlyOnce and EveryListedOption_IsParsedAndListedOnce fail with 'expected count: 281, actual count: 282'. The hardcoded count still does not match the table, so Curl.Cli's baseline stays red.

## Log

- 2026-10-02: filed proposed.
- 2026-10-02: proposed -> accepted.
