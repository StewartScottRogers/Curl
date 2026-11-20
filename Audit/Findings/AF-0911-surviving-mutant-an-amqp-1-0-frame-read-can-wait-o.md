---
id: AF-0911
title: Surviving mutant: an AMQP 1.0 frame read can wait on a bare 4-byte SIZE instead of refusing a malformed one
auditor: quality
severity: Medium
status: proposed
reason:
key: quality:Curl.Extended.Protocol.Amqp1.UnitLibrary/Frames/Amqp1FrameCodec.cs:TryRead-lt:surviving-mutant
reproduction: mutation Curl.Extended.Protocol.Amqp1.UnitLibrary/Frames/Amqp1FrameCodec.cs:37:<
task: none
tasks:
found: 2026-11-20
found-at: 1ce730765b795e99d4d034faf03432b41b5ec6a4
scorecard: 2026-11-20_0456.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0911 - Surviving mutant: an AMQP 1.0 frame read can wait on a bare 4-byte SIZE instead of refusing a malformed one

## Summary

Medium finding from the quality auditor at `Curl.Extended.Protocol.Amqp1.UnitLibrary/Frames/Amqp1FrameCodec.cs:37`: Surviving mutant: an AMQP 1.0 frame read can wait on a bare 4-byte SIZE instead of refusing a malformed one.

## Evidence

Location: `Curl.Extended.Protocol.Amqp1.UnitLibrary/Frames/Amqp1FrameCodec.cs:37`

Invoke-MutationTest -Library Curl.Extended.Protocol.Amqp1.UnitLibrary -MaxMutants 25 -Seed 912464: 'if (buffer.Length < 4)' mutated to '<= 4' survived. With exactly the 4 SIZE bytes buffered, the original reads SIZE and raises Amqp1MalformedEncodingException for a SIZE below 8. The mutant returns false and waits. A peer that sends such a SIZE and closes then ends as a receive failure instead of a malformed-reply one. No test feeds TryRead a 4-byte buffer.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extended.Protocol.Amqp1.UnitLibrary/Frames/Amqp1FrameCodec.cs:37:< -Member TryRead -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-20: filed proposed.
