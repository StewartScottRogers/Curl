---
id: AF-0562
title: Surviving mutant: the PEM pass phrase prompt can stop reading at the first key without a test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:ReadConsoleLine-ne:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44:!=
task: none
tasks:
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0562 - Surviving mutant: the PEM pass phrase prompt can stop reading at the first key without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44`: Surviving mutant: the PEM pass phrase prompt can stop reading at the first key without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44`

Mutant 'key.Key != ConsoleKey.Enter' -> 'key.Key == ConsoleKey.Enter' survived (this sample ran without the three failing ECH tests). The loop then ends before the first non-Enter key, so the pass phrase read is empty and an encrypted --key fails. This is the same untested member as AF-0534, with another operator.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44:!= -Member ReadConsoleLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
