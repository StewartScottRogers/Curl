---
id: BL-1265
title: Fix AF-0009: Two Curl.Cli tests hardcode an option count of 280 and fail against the 281 options in the table; the red baseline blocks mutation testing of Curl.Cli
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-02
---
# BL-1265 — Fix AF-0009: Two Curl.Cli tests hardcode an option count of 280 and fail against the 281 options in the table; the red baseline blocks mutation testing of Curl.Cli

## Goal

The defect the audit office reported as AF-0009 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0009 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0009-two-curl-cli-tests-hardcode-an-option-count-of-280.md`.

Location: `Curl.Cli.UnitTests/CommandLineNextGroupTests.cs:61`

Location: `Curl.Cli.UnitTests/CommandLineNextGroupTests.cs:61`

dotnet test Curl.Cli.UnitTests -c Release --filter TestCategory!=Integration: Failed 2, Passed 3632. `Assert.HasCount(280, CommandLineOptionTable.GlobalOptionLongNames.Concat(perGroup))` reports expected 280, actual 281. LibcurlSourceCodeOptionCoverageTests.EveryListedOption_IsParsedAndListedOnce:73 fails the same way (`Assert.HasCount(280, listed)`). Neither test says which option is the 281st, so one of them counts a magic number rather than the 'exactly once' its name promises. The mutation tool's baseline also fails, so Curl.Cli has no mutation score in this audit.

Reproduction, from the finding:

Run from the repository root:

```powershell
dotnet test Curl.Cli.UnitTests -c Release --filter "Name=OptionTable_EveryRow_IsClassifiedAsGlobalOrPerGroupExactlyOnce|Name=EveryListedOption_IsParsedAndListedOnce" -nologo
```

- Expected: Passed: 2.
- Actual: Failed: 2, expected count: 280, actual count: 281.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- 2026-10-02: No code change was needed. In this tree neither test holds a literal 280: `OptionTable_EveryRow_IsClassifiedAsGlobalOrPerGroupExactlyOnce` asserts `HasCount(CommandLineOptionTable.Rows.Count, ...)` and `EveryListedOption_IsParsedAndListedOnce` asserts `HasCount(parsed.Count, listed)`, both counting against the option table rather than a magic number, and each row check names the offending option. `git log -S "HasCount(280"` finds no commit that ever held the literal, so the audited tree most likely carried a planted defect or a local edit. The finding's reproduction gives Passed: 2; the full fast suite is green (Curl.Cli.UnitTests 3701 passed). The quality auditor's re-audit closes AF-0009.

## Log

- 2026-10-03: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. AF-0009 reproduction passes: both option-count tests count against the option table, no hardcoded 280
