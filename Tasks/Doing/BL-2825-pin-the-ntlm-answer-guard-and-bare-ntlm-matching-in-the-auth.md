---
id: BL-2825
title: Pin the NTLM-answer guard and bare NTLM matching in the authproblem tests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-19
completed:
---
# BL-2825 — Pin the NTLM-answer guard and bare NTLM matching in the authproblem tests

## Goal

The tests of `HttpProtocolHandler.SetsAuthProblem` fail if its `sentNtlmAuthorization` guard, its `Trim()` or its case-insensitive `NTLM` match is removed.

## Context

Found by the shift-end review (pr-test-analyzer) of 9f189b0af (BL-2823). `SetsAuthProblem` in `Curl.Protocol.Http.UnitLibrary/HttpProtocolHandler.cs` (~line 2765) is `(heldDigestNonce && !stale) || (sentNtlmAuthorization && challenges.Any(c => c.Trim().Equals("NTLM", OrdinalIgnoreCase)))`. The new test `ExecuteAsync_401ToAnNtlmAnswer_ReportsWhetherDigestStaysAvailable` (`Curl.Protocol.Http.UnitTests/HttpProtocolHandlerTests.KeptDigestChallenges.cs:133-154`) always sends an NTLM answer and always uses an exact-case `NTLM` with no padding. These mutants survive:

- Deleting the `sentNtlmAuthorization &&` guard. No row has a bare `NTLM` plus Digest in a 401 to a request without an NTLM answer.
- Dropping `Trim()`, or switching to an ordinal (case-sensitive) comparison. No row has `ntlm` or `NTLM ` with surrounding blanks.

libcurl 8.22.0's `Curl_input_ntlm` matches the scheme with `checkprefix` (case-insensitive) and skips blanks. So both behaviours are part of the drop-in contract.

## Acceptance criteria

- [ ] A test row sends a 401 with a bare `NTLM` and a Digest challenge to a request without an NTLM `Authorization`, and asserts `DigestStaysAvailable` is false. This holds when no Digest nonce is held.
- [ ] A test row with a lowercase or blank-padded bare `ntlm` challenge to an NTLM answer asserts `DigestStaysAvailable` is true.
- [ ] Each of the three mutants above makes at least one test fail, or the Log shows it is equivalent (for example, header values that arrive already trimmed). Check this by hand.
- [ ] `dotnet build` is clean and the fast tests pass.

## Notes

## Log

- 2026-10-19: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: a2210419 46cbe634 9c4f4471 3d836d5e.
