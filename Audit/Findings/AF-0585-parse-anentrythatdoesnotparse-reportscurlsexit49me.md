---
id: AF-0585
title: Parse_AnEntryThatDoesNotParse_ReportsCurlsExit49Message never checks exit 49
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitTests/ResolveOverridesTests.cs:Parse_AnEntryThatDoesNotParse_ReportsCurlsExit49Message:name-lies
reproduction: none
task: BL-4749
tasks: BL-4749
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0585 - Parse_AnEntryThatDoesNotParse_ReportsCurlsExit49Message never checks exit 49

## Summary

High finding from the quality auditor at `Curl.Networking.UnitTests/ResolveOverridesTests.cs:248`: Parse_AnEntryThatDoesNotParse_ReportsCurlsExit49Message never checks exit 49. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Networking.UnitTests/ResolveOverridesTests.cs:248`

Step 0 name-lies. The body asserts only overrides.ParseError's text; the exit code ResolveOverrides gives for it (ParseFailureCodeWith) is never checked.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/ResolveOverridesTests.cs -Pattern 'Parse_AnEntryThatDoesNotParse_ReportsCurlsExit49Message' -Context 0,11
```

- Expected: The body asserts CurlExitCode.SetoptOptionSyntax.
- Actual: Only Assert.AreEqual($"Could not parse CURLOPT_RESOLVE entry '{entry}'", overrides.ParseError);

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
