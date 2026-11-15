---
id: BL-5216
title: Fix AF-0767: Surviving mutant: a NATS line of exactly 65536 bytes can be refused with exit 8
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Nats.UnitLibrary]
requirement: none
created: 2026-11-14
completed: 2026-11-14
---
# BL-5216 — Fix AF-0767: Surviving mutant: a NATS line of exactly 65536 bytes can be refused with exit 8

## Goal

The defect the audit office reported as AF-0767 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0767 (High, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsWireReader.cs:82`

Location: `Curl.Extended.Protocol.Nats.UnitLibrary/NatsWireReader.cs:82`

Mutant 'if (line.Length > MaxLineLength)' -> '>=' survived (MaxLineLength = 65536, line 14). The over-limit tests (NatsWireTests ReadLineAsync_ALineOverTheLimit...) send 4 x 16384 + 'x', which is past the limit; no test sends a line of exactly the limit. The mutant changes a valid boundary line into exit 8, 'line too long'.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Nats.UnitLibrary/NatsWireReader.cs line 82` as the evidence above describes (the mutant operator is `>`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Nats.UnitLibrary/NatsWireReader.cs line 82`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Killed by `NatsWireTests.ReadLineAsync_ALineOfExactlyTheLimitBufferedBeforeItsEnd_IsReturned` and `ReadLineAsync_ALineOfExactlyTheLimitEndedInTheLastRead_IsReturned` (new): with `>=` both fail, with `>` all 84 pass. Test-only change; no production code changed.

## Log

- 2026-11-14: Created.
- 2026-11-14: Backlog -> Doing. Criteria at claim: bbec8eec 9742157d.
- 2026-11-14: Doing -> Done. Boundary tests added; mutant killed
