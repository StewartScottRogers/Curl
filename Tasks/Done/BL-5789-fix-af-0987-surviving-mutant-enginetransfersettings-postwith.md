---
id: BL-5789
title: Fix AF-0987: Surviving mutant: EngineTransferSettings.PostWithoutData can default to true, turning every CLI CoAP GET into a POST and failing MQTT with exit 43
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineTransferSettings.cs]
requirement: none
created: 2026-11-22
completed: 2026-11-22
---
# BL-5789 — Fix AF-0987: Surviving mutant: EngineTransferSettings.PostWithoutData can default to true, turning every CLI CoAP GET into a POST and failing MQTT with exit 43

## Goal

The defect the audit office reported as AF-0987 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0987 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/EngineTransferSettings.cs:57`

Location: `Curl.Engine.UnitLibrary/EngineTransferSettings.cs:57`

Seeded mutation run: 'bool PostWithoutData = false);' -> 'bool PostWithoutData = true);' survived. The default matters. Curl.Console/EasyOptionsFromCommandLine.TransferSettingsOf builds EngineTransferSettings without naming PostWithoutData, so every command-line transfer takes the default. TransferContextFactory copies it into the context: CoapRequest picks POST when context.PostWithoutData is set, and the MQTT handler refuses a POST without data with exit 43. No Curl.Engine test checks the default.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/EngineTransferSettings.cs line 57` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/EngineTransferSettings.cs line 57`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The mutant (default true) is killed by Curl.Console.UnitTests PostWithoutDataDefaultTests.TransferSettingsOf_PlainCommandLine_IsNotPostWithoutData, which builds settings via EasyOptionsFromCommandLine.TransferSettingsOf as the CLI does (internal to Curl.Console, hence that test project). Applied by hand it failed; reverted, it and all Curl.Console fast tests pass. No production code changed.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 1bf9485c 9742157d.
- 2026-11-22: Doing -> Done. Added PostWithoutDataDefaultTests in Curl.Console.UnitTests, which kills the default-true mutant.
