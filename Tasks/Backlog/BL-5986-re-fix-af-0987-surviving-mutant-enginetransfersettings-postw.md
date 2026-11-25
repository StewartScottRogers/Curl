---
id: BL-5986
title: Re-fix AF-0987: Surviving mutant: EngineTransferSettings.PostWithoutData can default to true, turning every CLI CoAP GET into a POST and failing MQTT with exit 43
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EngineTransferSettings.cs]
requirement: none
created: 2026-11-25
completed:
---
# BL-5986 — Re-fix AF-0987: Surviving mutant: EngineTransferSettings.PostWithoutData can default to true, turning every CLI CoAP GET into a POST and failing MQTT with exit 43

## Goal

The defect the audit office reported as AF-0987 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0987 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Re-fix: the earlier task(s) BL-5789 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-25 | 2026-11-25_1306.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site command: line 57, 'bool PostWithoutData = false' -> 'true', outcome survived. Runner's targeted mutation rerun on the clean audited commit: survived.

Location: `Curl.Engine.UnitLibrary/EngineTransferSettings.cs:57`

Location: `Curl.Engine.UnitLibrary/EngineTransferSettings.cs:57`

Seeded mutation run: 'bool PostWithoutData = false);' -> 'bool PostWithoutData = true);' survived. The default matters. Curl.Console/EasyOptionsFromCommandLine.TransferSettingsOf builds EngineTransferSettings without naming PostWithoutData, so every command-line transfer takes the default. TransferContextFactory copies it into the context: CoapRequest picks POST when context.PostWithoutData is set, and the MQTT handler refuses a POST without data with exit 43. No Curl.Engine test checks the default.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/EngineTransferSettings.cs line 57` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/EngineTransferSettings.cs line 57`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-25: Created.
