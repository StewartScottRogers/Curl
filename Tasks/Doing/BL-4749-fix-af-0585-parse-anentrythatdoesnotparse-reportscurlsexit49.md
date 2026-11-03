---
id: BL-4749
title: Fix AF-0585: Parse_AnEntryThatDoesNotParse_ReportsCurlsExit49Message never checks exit 49
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-11-03
completed:
---
# BL-4749 — Fix AF-0585: Parse_AnEntryThatDoesNotParse_ReportsCurlsExit49Message never checks exit 49

## Goal

The defect the audit office reported as AF-0585 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0585 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0585-parse-anentrythatdoesnotparse-reportscurlsexit49me.md`.

Location: `Curl.Networking.UnitTests/ResolveOverridesTests.cs:248`

Location: `Curl.Networking.UnitTests/ResolveOverridesTests.cs:248`

Step 0 name-lies. The body asserts only overrides.ParseError's text; the exit code ResolveOverrides gives for it (ParseFailureCodeWith) is never checked.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/ResolveOverridesTests.cs -Pattern 'Parse_AnEntryThatDoesNotParse_ReportsCurlsExit49Message' -Context 0,11
```

- Expected: The body asserts CurlExitCode.SetoptOptionSyntax.
- Actual: Only Assert.AreEqual($"Could not parse CURLOPT_RESOLVE entry '{entry}'", overrides.ParseError);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Added the ParseFailureCodeWith exit-49 assertion to the test (ResolveOverridesTests.cs); Networking fast tests pass (4018). Test-only change, no production code.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
