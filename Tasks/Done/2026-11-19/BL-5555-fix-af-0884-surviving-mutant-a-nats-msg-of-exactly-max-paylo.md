---
id: BL-5555
title: Fix AF-0884: Surviving mutant: a NATS MSG of exactly max_payload bytes can be refused with exit 8
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary/NatsMessageLine.cs]
requirement: none
created: 2026-11-19
completed: 2026-11-19
---
# BL-5555 — Fix AF-0884: Surviving mutant: a NATS MSG of exactly max_payload bytes can be refused with exit 8

## Goal

The defect the audit office reported as AF-0884 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0884 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsMessageLine.cs:43`

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsMessageLine.cs:43`

Mutant && total <= maxPayload -> && total < maxPayload in TryReadLengths survived every Curl.Extended.Protocol.Nats.UnitTests test (seed 446316). With it, a MSG or HMSG line whose total length equals the server's max_payload throws NatsException(WeirdServerReply, "NATS server sent an unexpected line: ..."), so a subscribe that receives a maximum-size message ends with exit 8 instead of writing the payload. The tests cover publishing exactly max_payload (ExecuteAsync_DataOfExactlyMaxPayload_PublishesIt) but never receiving it, and MessageLineParse_FourOrFiveFields_ReadsThem uses lengths well under the limit of 10.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Nats.UnitLibrary/NatsMessageLine.cs line 43` as the evidence above describes (the mutant operator is `<=`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Nats.UnitLibrary/NatsMessageLine.cs line 43`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by MessageLineParse_FourOrFiveFields_ReadsThem (new row "MSG a.b 1 10", length equals max_payload). Test-only change; no production code changed.

## Log

- 2026-11-19: Created.
- 2026-11-19: Backlog -> Doing. Criteria at claim: 3afc2d6e 9742157d.
- 2026-11-19: Doing -> Done. Added a max_payload-length MSG row that kills the mutant
