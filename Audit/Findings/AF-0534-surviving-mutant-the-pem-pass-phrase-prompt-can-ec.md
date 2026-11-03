---
id: AF-0534
title: Surviving mutant: the PEM pass phrase prompt can echo the first typed key to the terminal without a test failing
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:ReadConsoleLine-true:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44:true
task: BL-4640
tasks: BL-4640
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0534 - Surviving mutant: the PEM pass phrase prompt can echo the first typed key to the terminal without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44`: Surviving mutant: the PEM pass phrase prompt can echo the first typed key to the terminal without a test failing. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44`

Mutant 'Console.ReadKey(intercept: true)' -> 'Console.ReadKey(intercept: false)' in the loop initialiser of ReadConsoleLine survived (Curl.Networking.UnitTests green). With it the first character of the private key's pass phrase is echoed to the screen. ReadConsoleLine reads System.Console directly, so no test can reach it: the prompt has no injectable key source, and the no-echo behaviour is unchecked.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44:true -Member ReadConsoleLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-03 | 2026-11-03_0025.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: outcome survived at resolvedLine 44 ('Console.ReadKey(intercept: true)' -> 'Console.ReadKey(intercept: false)' for the first key); the baseline left out three failing ECH tests. Runner's targeted mutation rerun on the clean audited commit: survived.

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
