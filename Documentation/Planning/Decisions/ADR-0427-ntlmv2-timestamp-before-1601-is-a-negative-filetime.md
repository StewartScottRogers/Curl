# ADR-0427 — An NTLMv2 timestamp before 1601 is written as a negative FILETIME, not refused

- Status: Accepted
- Date: 2026-10-07
- Task: BL-1654
- Decided by Claude under Stewart's delegation.

## Context

`NtlmResponseComputation.WriteV2Blob` wrote the NTLMv2 blob's timestamp with
`DateTimeOffset.ToFileTime`, which throws `ArgumentOutOfRangeException` for a time before
1601-01-01 UTC. `NtlmChallengeAnswerer.Answer` given a `TimeProvider` reading such a time
let that exception escape, and neither `Answer` nor `ComputeV2` documents it. A real clock
never reads before 1601; the defect is an exception the public contract does not name.

curl 8.21.0's `Curl_ntlm_core_mk_ntlmv2_resp` computes the timestamp as
`((curl_off_t)time + 11644473600) * 10000000` on a signed 64-bit value and writes it little
endian, so for a time before 1601 it sends a negative FILETIME (two's complement) and
carries on.

## Decision

The blob's timestamp is `timestamp.UtcTicks - 504911232000000000` (the ticks of
1601-01-01 UTC): identical to `ToFileTime` from 1601 on, and the same negative value curl's
arithmetic gives before it. `ComputeV2` and `Answer` never throw for the clock's value.

## Why

Matching curl's arithmetic keeps the drop-in rule and removes the undocumented exception
without a branch, so the library keeps full coverage; every `DateTimeOffset` fits, since
`DateTimeOffset.MinValue` gives -504911232000000000. Documenting the exception instead would
make Curl fail where curl does not.
