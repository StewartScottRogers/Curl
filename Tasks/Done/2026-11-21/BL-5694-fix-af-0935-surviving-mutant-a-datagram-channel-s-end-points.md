---
id: BL-5694
title: Fix AF-0935: Surviving mutant: a datagram channel's end points can be re-recorded on every datagram, not once
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs]
requirement: none
created: 2026-11-21
completed: 2026-11-21
---
# BL-5694 — Fix AF-0935: Surviving mutant: a datagram channel's end points can be re-recorded on every datagram, not once

## Goal

The defect the audit office reported as AF-0935 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0935 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs:65`

Location: `Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs:65`

Seed 812510 sample: _recorded = true; mutated to _recorded = false; survived. The class comment says the end points are recorded again once a datagram has been received; the mutant re-records on every receive, so a channel that moves again changes the reported end points (%{remote_ip}, %{local_port}). No test receives twice after a move.

Reproduction: the audit office's mutation test changed `Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs line 65` as the evidence above describes (the mutant operator is `true`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Engine.UnitLibrary/EndPointRecordingDatagramConnector.cs line 65`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by the new test `EndPointRecordingDatagramConnectorTests.ReceiveAsync_ChannelMovesAgainAfterTheFirstReceive_KeepsTheEndPointsRecordedAtTheFirst` (a channel that moves on every receive; with `_recorded = false` it fails, reverted it passes). Production code unchanged. Build clean; Curl.Engine fast tests 3136 passed, 0 failed.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: 391e436c 9742157d.
- 2026-11-21: Doing -> Done. Mutant killed by a new test; fast tests green
