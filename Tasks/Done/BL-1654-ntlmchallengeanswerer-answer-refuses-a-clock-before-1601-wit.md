---
id: BL-1654
title: NtlmChallengeAnswerer.Answer refuses a clock before 1601 with a documented failure instead of ArgumentOutOfRangeException
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Ntlm.UnitLibrary, Curl.Ntlm.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1654 — NtlmChallengeAnswerer.Answer refuses a clock before 1601 with a documented failure instead of ArgumentOutOfRangeException

## Goal

`NtlmChallengeAnswerer.Answer` given a `TimeProvider` whose time is before 1601-01-01 UTC, on a challenge with `NegotiateExtendedSessionSecurity`, no longer lets `ArgumentOutOfRangeException` escape from `DateTimeOffset.ToFileTime` undocumented: it either clamps the NTLMv2 timestamp as curl's `time(NULL)` arithmetic would, or documents the exception on `Answer` and `NtlmResponseComputation.ComputeV2`.

## Context

- Found by BL-1503's adversarial tests. Input: `new NtlmChallengeAnswerer(clockAt(DateTimeOffset.MinValue), random).Answer(new NtlmChallengeMessage(NtlmNegotiateFlags.NegotiateExtendedSessionSecurity, new byte[8], [], [], []), "u", "pw")`.
- Happens in `NtlmResponseComputation.WriteV2Blob` (`timestamp.ToFileTime()`); `ComputeV2`'s doc promises only `ArgumentException` for wrong-length challenges.
- A real clock never reads before 1601, so not a crash in practice; the defect is an exception the public contract does not name. Decide by curl 8.21.0's `Curl_ntlm_core_mk_ntlmv2_resp` (it computes `(tw + 11644473600) * 10000000` on a 64-bit value).

## Acceptance criteria

- [x] A test in `Curl.Ntlm.UnitTests` named for the case pins the chosen behaviour (no undocumented exception escapes `Answer`).
- [x] `dotnet test Curl.Ntlm.UnitTests --filter "TestCategory!=Integration"` passes and the library keeps 100% line and branch coverage.

## Notes

- Decided (ADR-0426): match curl's signed `(time + 11644473600) * 10000000` arithmetic. `WriteV2Blob` now writes `timestamp.UtcTicks - 504911232000000000`, identical to `ToFileTime` from 1601 on and a negative FILETIME before it, so `Answer` and `ComputeV2` never throw for the clock's value. Documenting the exception instead would make Curl fail where curl does not.
- Test: `NtlmChallengeAnswererTests.Answer_ClockBefore1601_SendsNegativeFileTimeInsteadOfThrowing` (the BL-1503 input). Measure-CodeQuality: Curl.Ntlm.UnitLibrary 100% line, 100% branch, 0 failing members.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. An NTLMv2 clock before 1601 writes a negative FILETIME as curl does, instead of throwing ArgumentOutOfRangeException
