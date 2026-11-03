---
id: BL-4640
title: Fix AF-0534: Surviving mutant: the PEM pass phrase prompt can echo the first typed key to the terminal without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs]
requirement: none
created: 2026-11-03
completed:
---
# BL-4640 — Fix AF-0534: Surviving mutant: the PEM pass phrase prompt can echo the first typed key to the terminal without a test failing

## Goal

The defect the audit office reported as AF-0534 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0534 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0534-surviving-mutant-the-pem-pass-phrase-prompt-can-ec.md`.

Location: `Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44`

Location: `Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44`

Mutant 'Console.ReadKey(intercept: true)' -> 'Console.ReadKey(intercept: false)' in the loop initialiser of ReadConsoleLine survived (Curl.Networking.UnitTests green). With it the first character of the private key's pass phrase is echoed to the screen. ReadConsoleLine reads System.Console directly, so no test can reach it: the prompt has no injectable key source, and the no-echo behaviour is unchecked.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44:true -Member ReadConsoleLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
