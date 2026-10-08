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
completed:
---
# BL-1654 — NtlmChallengeAnswerer.Answer refuses a clock before 1601 with a documented failure instead of ArgumentOutOfRangeException

## Goal

`NtlmChallengeAnswerer.Answer` given a `TimeProvider` whose time is before 1601-01-01 UTC, on a challenge with `NegotiateExtendedSessionSecurity`, no longer lets `ArgumentOutOfRangeException` escape from `DateTimeOffset.ToFileTime` undocumented: it either clamps the NTLMv2 timestamp as curl's `time(NULL)` arithmetic would, or documents the exception on `Answer` and `NtlmResponseComputation.ComputeV2`.

## Context

- Found by BL-1503's adversarial tests. Input: `new NtlmChallengeAnswerer(clockAt(DateTimeOffset.MinValue), random).Answer(new NtlmChallengeMessage(NtlmNegotiateFlags.NegotiateExtendedSessionSecurity, new byte[8], [], [], []), "u", "pw")`.
- Happens in `NtlmResponseComputation.WriteV2Blob` (`timestamp.ToFileTime()`); `ComputeV2`'s doc promises only `ArgumentException` for wrong-length challenges.
- A real clock never reads before 1601, so not a crash in practice; the defect is an exception the public contract does not name. Decide by curl 8.21.0's `Curl_ntlm_core_mk_ntlmv2_resp` (it computes `(tw + 11644473600) * 10000000` on a 64-bit value).

## Acceptance criteria

- [ ] A test in `Curl.Ntlm.UnitTests` named for the case pins the chosen behaviour (no undocumented exception escapes `Answer`).
- [ ] `dotnet test Curl.Ntlm.UnitTests --filter "TestCategory!=Integration"` passes and the library keeps 100% line and branch coverage.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
