---
id: BL-5672
title: Re-fix AF-0916: Surviving mutant: WriteGate.RunExclusive can leave its flow marked as holding the gate, so later writes bypass it
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/WriteGate.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5672 — Re-fix AF-0916: Surviving mutant: WriteGate.RunExclusive can leave its flow marked as holding the gate, so later writes bypass it

## Goal

The defect the audit office reported as AF-0916 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0916 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Re-fix: the earlier task(s) BL-5651 reached Done, and a later re-audit by the quality auditor found the reproduction still reproduces:

- 2026-11-21 | 2026-11-21_0114.md | reproduces: yes (runner rerun on clean commit) | Ran the -Site Curl.Engine.UnitLibrary/WriteGate.cs:80:true command: resolvedLine 80, 'heldByThisFlow.Value = true;' -> 'heldByThisFlow.Value = false;', outcome survived (two unrelated baseline failures excluded). Runner's targeted mutation rerun on the clean audited commit: survived.

Location: `Curl.Engine.UnitLibrary/WriteGate.cs:80`

Location: `Curl.Engine.UnitLibrary/WriteGate.cs:80`

In RunExclusive's finally block `heldByThisFlow.Value = false;` mutated to `= true;` survived Curl.Engine.UnitTests. After one exclusive synchronous write the flow believes it still holds the permit, so every later WriteExclusiveAsync/RunExclusive in that flow writes straight to the shared stream without taking the permit: parallel transfers (-Z) can interleave their output bytes. No test runs an exclusive write and then checks that a later write from the same flow still waits for the gate.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/WriteGate.cs line 80` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/WriteGate.cs line 80`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
