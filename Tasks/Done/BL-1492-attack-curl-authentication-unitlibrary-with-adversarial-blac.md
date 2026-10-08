---
id: BL-1492
title: Attack Curl.Authentication.UnitLibrary with adversarial black-box tests in Curl.Authentication.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1454, BL-1710]
touches: [Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-08
---
# BL-1492 — Attack Curl.Authentication.UnitLibrary with adversarial black-box tests in Curl.Authentication.UnitTests

## Goal

`Curl.Authentication.UnitTests` gains adversarial black-box tests that attack `Curl.Authentication.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1454, BL-1458.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Authentication.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: challenge-header parsing for Basic, Digest, Negotiate, NTLM, Bearer and AWS SigV4: quoted strings with escapes and unterminated quotes, duplicate and unknown parameters, unknown Digest algorithms and qop values, empty and multi-kilobyte nonces, several challenges on one line; credentials with a colon in the user name, an empty password, non-ASCII and control characters.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Authentication.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Authentication.UnitTests -warnaserror` is clean and `dotnet test Curl.Authentication.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Authentication.UnitTests/` and this task file.

## Notes

- Test count (fast run, `TestCategory!=Integration`): 819 before, 891 after (72 new cases in `AuthenticationAdversarialTests`). No test is over 1 MiB, so none is Integration.
- Boundaries: `DigestCreateAuthorization_NonceOfBoundaryLength_EchoesTheNonce` (0, 1, 1023 chars), `DigestCreateAuthorization_NoncePastTheValueLimit_EchoesItsFirst1023Characters` (1024, 8 KiB, 64 KiB: truncated to 1023 as curl's DIGEST_MAX_CONTENT_LENGTH does), `DigestCreateAuthorization_ManyChallengesOnOneLine_AnswersTheFirst` (1000 challenges), `BasicCreateAuthorization_BoundaryCredentials_EncodesUserColonPassword`, `NetrcFind_EntryAfterTenThousandOtherMachines_IsFound`, `NetrcFind_LongQuotedPassword_ReturnsAnOutcomeWithoutThrowing`, `NetrcFind_BoundaryHostName_DoesNotThrow`.
- Malformed input: `DigestCreateAuthorization_MalformedChallenge_AnswersOrDeclinesWithoutThrowing` (unterminated quotes, trailing backslash, unknown algorithm and qop, duplicate and unknown parameters, control characters), `DigestCreateAuthorization_HostileCredentials_Answers`, `DigestRepeatAuthorization_MalformedSentHeader_DoesNotThrow`, `BasicCreateAuthorization_MalformedBasicChallenge_StillAnswers`, `NetrcFind_MalformedText_ReturnsAnOutcomeWithoutThrowing`, `SpnegoDecode_MalformedToken_ThrowsOnlySpnegoTokenException`, `SpnegoDecode_EveryTruncationOfAnEncodedResponse_ThrowsOnlySpnegoTokenException`.
- Invalid partitions: `BasicCreateAuthorization_NoBasicChallenge_Declines` (empty, commas only, `Basicx` prefix), plus the unknown-algorithm/qop and wrong-token-type rows above.
- State and concurrency: `DigestCreateAuthorization_SameInputRepeated_GivesTheSameAnswer`, `DigestCreateAuthorization_CalledConcurrently_GivesTheSameAnswerEveryTime` (64 parallel calls on one authenticator). The stateful Negotiate/NTLM context keying is behind security-context fakes already covered by their own suites; not re-attacked here.
- Oracle: each type's documented contract (Digest's 1023-character value limit, SpnegoTokenException as the only refusal, NetrcLookupResult outcomes). No new curl measurement was needed: every pinned value is either RFC arithmetic (Basic base64) or the library's documented curl-matching limit. AWS SigV4 challenge parsing does not exist (SigV4 is not challenge-driven), so not applicable.
- Defects found: none; every attack got an answer or the documented refusal, so no follow-up task was filed.

## Log

- 2026-10-06: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Done. 72 adversarial cases added to Curl.Authentication.UnitTests; no defects found
