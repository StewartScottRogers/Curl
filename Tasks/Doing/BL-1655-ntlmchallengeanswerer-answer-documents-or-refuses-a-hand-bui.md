---
id: BL-1655
title: NtlmChallengeAnswerer.Answer documents or refuses a hand-built challenge whose ServerChallenge is not 8 bytes
priority: Low
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Ntlm.UnitLibrary, Curl.Ntlm.UnitTests]
requirement: none
created: 2026-10-07
completed:
---
# BL-1655 — NtlmChallengeAnswerer.Answer documents or refuses a hand-built challenge whose ServerChallenge is not 8 bytes

## Goal

`NtlmChallengeAnswerer.Answer` given an `NtlmChallengeMessage` built by hand whose `ServerChallenge` is not 8 bytes either documents the `ArgumentException` it throws in its `<exception>` doc comment or refuses through a typed failure; today the exception comes from `NtlmResponseComputation` and `Answer`'s doc does not name it.

## Context

- Found by BL-1503's adversarial tests. Input: `answerer.Answer(new NtlmChallengeMessage(NtlmNegotiateFlags.None, new byte[7], [], [], []), "u", "pw")` throws `ArgumentException` ("NTLM needs 8 bytes here").
- `NtlmChallengeMessage.Decode` always yields 8 bytes, so only a caller building the record by hand reaches it. Low priority: documentation-level contract gap, not a crash on wire input.

## Acceptance criteria

- [ ] `Answer`'s doc comment names the exception (or the refusal is typed), and a test in `Curl.Ntlm.UnitTests` named for the case pins it for 0, 7 and 9 bytes.
- [ ] `dotnet test Curl.Ntlm.UnitTests --filter "TestCategory!=Integration"` passes.

## Notes

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
