---
id: AF-0951
title: Surviving mutant: an .http header line starting with ':' can be accepted instead of refused with exit 2
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extension.HttpFile.UnitLibrary/HttpFileParser.cs:HeaderOrThrow-gt:surviving-mutant
reproduction: mutation Curl.Extension.HttpFile.UnitLibrary/HttpFileParser.cs:126:>
task: none
tasks:
found: 2026-11-21
found-at: 8d41979abc77036a1cf5ff183027379b16dc1eea
scorecard: 2026-11-21_1740.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0951 - Surviving mutant: an .http header line starting with ':' can be accepted instead of refused with exit 2

## Summary

High finding from the quality auditor at `Curl.Extension.HttpFile.UnitLibrary/HttpFileParser.cs:126`: Surviving mutant: an .http header line starting with ':' can be accepted instead of refused with exit 2. Reported by an auditor flagged unreliable in 2026-11-21_1740.md.

## Evidence

Location: `Curl.Extension.HttpFile.UnitLibrary/HttpFileParser.cs:126`

Lines 125-128: HeaderOrThrow(line) => line.IndexOf(':') > 0 ? line : throw new ArgumentExpansionException($"http-file: '{line}' is not a header line", CurlExitCode.FailedInit); mutated to >= 0 survived all 43 HttpFile.UnitTests (all read). A header line with an empty name such as ':x' is then passed on as -H ':x' instead of failing the run with exit 2. The only refusal test, Parse_HeaderLineWithoutAColon_FailsWithExit2, uses '?a=1' (no colon at all), so the index-0 case is unpinned.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.HttpFile.UnitLibrary/HttpFileParser.cs:126:> -Member HeaderOrThrow -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a test refuses ':x' with exit 2
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-21: filed proposed.
