---
id: BL-4640
title: Fix AF-0534: Surviving mutant: the PEM pass phrase prompt can echo the first typed key to the terminal without a test failing
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/TerminalPemPassPhrasePrompt.cs, Curl.Networking.UnitTests/TerminalPemPassPhrasePromptTests.cs]
requirement: none
created: 2026-11-03
completed: 2026-11-03
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Fix: `ReadConsoleLine` now takes its console as parameters (`inputRedirected`, a `TextReader` and an internal `ReadConsoleKey(bool intercept)` delegate) and is covered; the private `ReadProcessConsoleLine`, excluded under ADR-0083, only passes `Console.IsInputRedirected`, `Console.In` and `Console.ReadKey`. The mutated `readKey(intercept: true)` loop initialiser is still on line 44 of member `ReadConsoleLine`, so the finding's site and member still name it.
- New `TerminalPemPassPhrasePromptTests` checks that every key read, the first included, is intercepted, and that redirected input reads one line and no key. Added that new test file to `touches` (a new file, no other task holds it).
- Reproduction: a lane may not run `Audit/Tools`, so the mutant was applied by hand (line 44's first `intercept: true` -> `false`): `ReadConsoleLine_FromConsole_InterceptsEveryKeyIncludingTheFirst` failed (killed), and the file was restored. The re-audit runs the finding's own command.
- `dotnet build` clean; fast tests: 115 test projects passed, none failed. Measure-CodeQuality for Curl.Networking.UnitLibrary: 0 failing members. Platform-neutral: the tests use no file system, path or OS text.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. The PEM pass phrase prompt's key reading is injectable and a test kills the intercept mutant
