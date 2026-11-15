---
id: BL-5221
title: Fix AF-0772: Surviving mutant: a failed proxy selection can be reported as success and the transfer go on
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-11-14
completed:
---
# BL-5221 — Fix AF-0772: Surviving mutant: a failed proxy selection can be reported as success and the transfer go on

## Goal

The defect the audit office reported as AF-0772 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0772 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:239`

Location: `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs:239`

Mutant 'return false;' -> 'return true;' survived in the branch taken when TransferProxySelection.TrySelect(...) fails. Its caller, Curl.Console/CurlCommandRunner.cs:4556, proceeds when the method returns true, so a proxy curl refuses would no longer end the transfer with its exit code. The Engine tests of this method (TransferPerformerTests.Setup.cs, TransferPerformerTests.FeatureMask.cs) cover only the not-built-in mask, the scheme refusal and successful selection, never a TrySelect failure.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs line 239` as the evidence above describes (the mutant operator is `false`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs line 239`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-14: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: a5b5508a 9742157d.
