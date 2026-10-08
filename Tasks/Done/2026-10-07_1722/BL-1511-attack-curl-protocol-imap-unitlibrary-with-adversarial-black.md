---
id: BL-1511
title: Attack Curl.Protocol.Imap.UnitLibrary with adversarial black-box tests in Curl.Protocol.Imap.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1477]
touches: [Curl.Protocol.Imap.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1511 — Attack Curl.Protocol.Imap.UnitLibrary with adversarial black-box tests in Curl.Protocol.Imap.UnitTests

## Goal

`Curl.Protocol.Imap.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Imap.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1477.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Imap.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: literal `{n}` sizes of 0, huge and negative, quoted strings with escapes, tagged replies that do not match the command's tag, untagged replies interleaved, CRLF injection through mailbox names and `-X`, and UIDs at 0, 2^32-1 and past.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Imap.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Imap.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Imap.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Imap.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Protocol.Imap.UnitTests/ImapProtocolHandlerAdversarialTests.cs`,
driving the one public type, `ImapProtocolHandler`, through `ScriptedConnection` and a
test-local thread-safe connector. Oracle: the handler's documented contract (curl 8.21.0
as measured in BL-553..559) and curl's `lib/imap.c` reading; no new curl measurement was
needed, since every attack lands on behaviour the existing measured tests already pin.

- **Boundaries:** `FetchLiteralOfZeroBytes_SucceedsWithNothingWrittenAndLogsOut`,
  `FetchLiteralHugeThenServerCloses_FailsWithExit18WithoutAllocatingIt` (long.MaxValue,
  2^32), `UidAtAndPastItsLimits_IsSentAsTypedAndFetched` (0, 2^32-1, 2^32, past long),
  `GreetingLineOneByteUnderTheLimit_IsRead`, `GreetingLineAtTheLimit_FailsWithExit100BeforeSendingAnything`,
  `CapabilityLiteralPastTheLineLimit_FailsWithExit100WithoutReadingIt`.
- **Malformed input:** `FetchLiteralSizeNotDigitsAndBrace_FailsWithExit8AfterLogout`
  (past long, negative, empty, unclosed, space, sign, hex),
  `MailboxDecodingToAControlByte_FailsWithExit3AndInjectsNothing` (CRLF, LF, NUL),
  `CustomCommandHoldingALineBreak_FailsWithExit3AndInjectsNothing` (encoded and raw CRLF,
  LF, NUL through `-X`), `MailboxOfQuotesAndBackslashes_IsQuotedWithEachEscaped`,
  `TaggedLineNotMatchingTheCommandsTag_IsIgnored`, `OnlyForeignTagsThenServerCloses_FailsWithExit56`,
  `UntaggedLinesInterleavedBeforeTheFetch_AreSkipped`,
  `FetchLineWithAQuotedBraceBeforeTheLiteral_ReadsTheQuotedBraceAsTheSize`.
- **Invalid partitions:** `ContinuationAnsweringCapability_FailsWithExit8`,
  `ContinuationAnsweringFetch_FailsWithExit8`, `GreetingNotOkOrPreauth_FailsWithExit8BeforeSendingAnything`
  (BAD, NO, BYE), `FetchCompletedWithNoUntaggedFetch_FailsWithExit78AfterLogout`,
  `ServerClosesRightAfterTheGreeting_FailsWithExit56`, `ServerSendsNothing_FailsWithExit56WithoutSendingAnything`.
  Malformed URL parameters (unknown, repeated, no `=`) are already pinned in
  `ImapProtocolHandlerFetchTests`, so not repeated.
- **State and concurrency:** `EveryReplyByteInItsOwnRead_FetchesTheSameMessage`,
  `ConversationSplitInTwoAtEveryOffset_FetchesTheSameMessage`,
  `ServerClosesAtEveryOffsetInsideTheLiteral_FailsWithExit18AndWritesWhatArrived`,
  `SameHandlerTwiceInARow_FetchesBothWithTagsStartingAgain`,
  `SameHandlerOnSixteenTransfersAtOnce_EachFetchesItsOwnMessage`. Time and cancellation:
  not applicable through this surface - the handler takes no `TimeProvider`, and
  `ScriptedConnection` never observes a token, so a cancellation test would only test the fake.
- **Defects found:** none. Two first-draft expectations were wrong, not the library: `+x`
  is not a continuation (curl's is `+` and exactly one character before the LF, i.e.
  `+\r`), and a tagged line before the greeting is ignored like any foreign tag.
- **Test count:** 425 before, 475 after (`dotnet test Curl.Protocol.Imap.UnitTests --filter "TestCategory!=Integration"`).
  No input is over 1 MiB, so nothing is in `Integration`.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Protocol.Imap.UnitTests attacks the IMAP handler with 50 adversarial black-box tests; no defects found
