---
id: BL-5557
title: Fix AF-0886: Surviving mutant: the multi callback queue can reset its arrival signal while callbacks are still queued
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/MultiCallbackQueue.cs]
requirement: none
created: 2026-11-19
completed:
---
# BL-5557 — Fix AF-0886: Surviving mutant: the multi callback queue can reset its arrival signal while callbacks are still queued

## Goal

The defect the audit office reported as AF-0886 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0886 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124`

Location: `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs:124`

Mutant if (queued.Count == 0 && arrived.Task.IsCompleted) -> if (queued.Count == 0 || arrived.Task.IsCompleted) in TryDequeue survived all Curl.Engine.UnitTests (seed 446316). With it, taking one callback while others are still queued replaces the completed arrived signal and resets queuedEvent, so a perform waiting on the signal does not wake for the callbacks left in the queue until another one is enqueued. That decides when a transfer's callbacks run, and can stall them. MultiCallbackQueueTests never queue two callbacks before one is taken.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs line 124` as the evidence above describes (the mutant operator is `&&`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/MultiCallbackQueue.cs line 124`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: b35db386 9742157d.
