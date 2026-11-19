---
id: BL-5544
title: Fix AF-0880: Surviving mutant: clearing a URL handle's scheme can leave it marked as guessed
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/UrlPartSetter.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5544 — Fix AF-0880: Surviving mutant: clearing a URL handle's scheme can leave it marked as guessed

## Goal

The defect the audit office reported as AF-0880 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0880 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:43`

Location: `Curl.Engine.UnitLibrary/UrlPartSetter.cs:43`

Mutant 'parts => parts with { Scheme = null, SchemeGuessed = true }' (from 'false') survived in the Engine sample and in the targeted -Site run. SchemeGuessed decides whether curl_url_get under CURLU_NO_GUESS_SCHEME drops the scheme (ManagedCurlUrlHandle.cs:154, 214) and lets an unsupported scheme through (line 410). So a handle whose scheme was cleared and then read with a default scheme would print a different URL. No test checks SchemeGuessed after clearing CURLUPART_SCHEME.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/UrlPartSetter.cs line 43` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/UrlPartSetter.cs line 43`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `UrlPartSetterClearTests.Cleared_SchemeOfAGuessedSchemeHandle_ClearsTheSchemeAndTheGuessedMark` (mutant applied by hand: it failed; reverted: all 3103 fast Engine tests pass). The mutant is not visible through `ManagedCurlUrlHandle` (a null scheme reads as NoScheme either way), so the test calls `UrlPartSetter.Cleared` directly. Production code unchanged, so no quality run needed.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 4777333f 9742157d.
- 2026-11-19: Doing -> Done. UrlPartSetterClearTests kills the mutant
