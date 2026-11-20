---
id: BL-5626
title: Fix AF-0911: Surviving mutant: an AMQP 1.0 frame read can wait on a bare 4-byte SIZE instead of refusing a malformed one
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extended.Protocol.Amqp1.UnitLibrary/Frames/Amqp1FrameCodec.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5626 — Fix AF-0911: Surviving mutant: an AMQP 1.0 frame read can wait on a bare 4-byte SIZE instead of refusing a malformed one

## Goal

The defect the audit office reported as AF-0911 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0911 (Medium, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Extended.Protocol.Amqp1.UnitLibrary/Frames/Amqp1FrameCodec.cs:37`

Location: `Curl.Extended.Protocol.Amqp1.UnitLibrary/Frames/Amqp1FrameCodec.cs:37`

Invoke-MutationTest -Library Curl.Extended.Protocol.Amqp1.UnitLibrary -MaxMutants 25 -Seed 912464: 'if (buffer.Length < 4)' mutated to '<= 4' survived. With exactly the 4 SIZE bytes buffered, the original reads SIZE and raises Amqp1MalformedEncodingException for a SIZE below 8. The mutant returns false and waits. A peer that sends such a SIZE and closes then ends as a receive failure instead of a malformed-reply one. No test feeds TryRead a 4-byte buffer.

Reproduction: the audit office's mutation test changed `Curl.Extended.Protocol.Amqp1.UnitLibrary/Frames/Amqp1FrameCodec.cs line 37` as the evidence above describes (the mutant operator is `<`), and every test still passed. Check it by hand, not with the audit tools: make that one change, run the library's `.UnitTests` project, see a test fail, and revert the change. The audit office reruns its mutation test on the re-audit.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] With the mutant the evidence describes applied by hand at `Curl.Extended.Protocol.Amqp1.UnitLibrary/Frames/Amqp1FrameCodec.cs line 37`, at least one fast test fails, and with it reverted they all pass; Notes name the test that kills it.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: 864d02a6 9742157d.
