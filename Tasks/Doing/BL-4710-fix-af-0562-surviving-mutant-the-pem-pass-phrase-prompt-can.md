---
id: BL-4710
title: Fix AF-0562: Surviving mutant: the PEM pass phrase prompt can stop reading at the first key without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4710 — Fix AF-0562: Surviving mutant: the PEM pass phrase prompt can stop reading at the first key without a test failing

## Goal

The defect the audit office reported as AF-0562 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0562 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0562-surviving-mutant-the-pem-pass-phrase-prompt-can-st.md`.

Location: `Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44`

Location: `Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44`

Mutant 'key.Key != ConsoleKey.Enter' -> 'key.Key == ConsoleKey.Enter' survived (this sample ran without the three failing ECH tests). The loop then ends before the first non-Enter key, so the pass phrase read is empty and an encrypted --key fails. This is the same untested member as AF-0534, with another operator.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs:44:!= -Member ReadConsoleLine -ExcludeBaselineFailures -TimeoutSeconds 600
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
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
