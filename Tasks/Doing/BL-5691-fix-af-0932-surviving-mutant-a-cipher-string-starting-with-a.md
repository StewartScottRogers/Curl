---
id: BL-5691
title: Fix AF-0932: Surviving mutant: a cipher string starting with a separator can be read as one element
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary/OpenSslCipherString.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5691 — Fix AF-0932: Surviving mutant: a cipher string starting with a separator can be read as one element

## Goal

The defect the audit office reported as AF-0932 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0932 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:195`

Location: `Curl.Networking.UnitLibrary/OpenSslCipherString.cs:195`

Seed 812510 sample: var element = end < 0 ? _value[_position..] : _value[_position..end]; mutated to end <= 0 survived. With a separator at index 0 (a --ciphers value starting with ':', ' ', ';' or ','), the mutant takes the whole rest as one selection, changing the parsed list and whether curl refuses it. No test starts a cipher string with a separator.

Reproduction: the audit office's mutation test changed `Curl.Networking.UnitLibrary/OpenSslCipherString.cs line 195` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Networking.UnitLibrary/OpenSslCipherString.cs line 195`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 3d257580 9742157d.
