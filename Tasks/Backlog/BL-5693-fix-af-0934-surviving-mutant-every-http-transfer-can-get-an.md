---
id: BL-5693
title: Fix AF-0934: Surviving mutant: every HTTP transfer can get an Alt-Svc cache without --alt-svc
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5693 — Fix AF-0934: Surviving mutant: every HTTP transfer can get an Alt-Svc cache without --alt-svc

## Goal

The defect the audit office reported as AF-0934 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0934 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:391`

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:391`

Seed 812510 sample: if (http.AltSvc is not null && IsHttpUrl(...)) mutated to || survived. AltSvcTransferCache.OpenAsync accepts a null AltSvc (file = http.AltSvc ?? string.Empty), so the mutant gives every HTTP transfer an in-memory Alt-Svc cache, and Alt-Svc headers would move later connections of the run although curl ignores them without --alt-svc. No test checks that no cache opens without the option.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs line 391` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs line 391`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
