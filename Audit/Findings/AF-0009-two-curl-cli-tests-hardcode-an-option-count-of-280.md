---
id: AF-0009
title: Two Curl.Cli tests hardcode an option count of 280 and fail against the 281 options in the table; the red baseline blocks mutation testing of Curl.Cli
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Cli.UnitTests/CommandLineNextGroupTests.cs:OptionTable_EveryRow_IsClassifiedAsGlobalOrPerGroupExactlyOnce:name-lies
task: none
found: 2026-10-02
found-at: 337ed10b42ddd4d09991deaecb10826c2dedba00
scorecard: 2026-10-02_1400.md
closed:
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

## Log

- 2026-10-02: filed proposed.
