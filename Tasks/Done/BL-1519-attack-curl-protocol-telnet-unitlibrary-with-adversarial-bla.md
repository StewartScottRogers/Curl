---
id: BL-1519
title: Attack Curl.Protocol.Telnet.UnitLibrary with adversarial black-box tests in Curl.Protocol.Telnet.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1485]
touches: [Curl.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1519 — Attack Curl.Protocol.Telnet.UnitLibrary with adversarial black-box tests in Curl.Protocol.Telnet.UnitTests

## Goal

`Curl.Protocol.Telnet.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Telnet.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1485.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Telnet.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: IAC escaping (`IAC IAC`, IAC at the end of a buffer), option-negotiation loops, `SB` without `SE`, unknown options, and `-t` option values that are malformed, empty or very long.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Telnet.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Telnet.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Telnet.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Protocol.Telnet.UnitTests/TelnetProtocolHandlerAdversarialTests.cs`, through `TelnetProtocolHandler` and the existing `ScriptedConnection` fake only (no internals, no network). Oracle: curl 8.21.0's `lib/telnet.c` receive state machine and the handler's documented contract; no new command-line output was pinned, so nothing needed measuring with `Record-CurlExchange.ps1`. `-t` values were already attacked hard by `TelnetProtocolHandlerTelnetOptionTests` (empty, 1000/1001 characters, malformed `WS`, unknown names, non-ASCII), so only the gaps were added.

- **Boundaries** - `ExecuteAsync_LoneIacAsTheLastByteBeforeClose_WritesTheDataBeforeItAndExitsOk`, `ExecuteAsync_IacIacSplitAcrossTwoReads_WritesOneFFBetweenTheData`, `ExecuteAsync_CarriageReturnEndsOneReadAndNulStartsTheNext_DropsTheNul`, `ExecuteAsync_CommandCutShortByClose_SendsNothingAndExitsOk` (4 rows), `ExecuteAsync_NegotiationForAnOptionNumberCurlDoesNotKnow_IsRefusedThenOffersFollow` (options 200 and 255), `ExecuteAsync_TerminalTypeSubnegotiationLongerThanCurlsBuffer_IsAnsweredOnce`.
- **Malformed input** - `ExecuteAsync_SubnegotiationWithoutSeBeforeClose_SwallowsTheRestAndExitsOk`, `ExecuteAsync_CommandThatNeedsNoReply_IsRemovedFromTheData` (SE outside SB, command bytes 0, 128, 239, IP), `ExecuteAsync_SubnegotiationBrokenByAnotherCommand_ExitsWith56AndAnswersNothing` (3 rows), `ExecuteAsync_SubnegotiationForAnOptionCurlNeverAnswers_IsIgnored` (3 rows), `ExecuteAsync_OptionWithAnEmptyName_ExitsBeforeSendingAByte` (`-t ""`, `=`, `==`), `ExecuteAsync_TerminalTypeValueHoldingEquals_SendsEverythingAfterTheFirstEquals`.
- **Invalid partitions** (server replies in states that do not expect them) - `ExecuteAsync_DoForAnUnknownOptionRepeated_IsRefusedEachTime`, `ExecuteAsync_DisableForAnOptionNeverEnabled_IsNotAnswered` (3 rows), `ExecuteAsync_ServerRefusesEveryOffer_SendsNothingMore`, `ExecuteAsync_TerminalTypeSubnegotiationBeforeAnyNegotiation_IsAnsweredWithoutOffers`.
- **State and concurrency** - `ExecuteAsync_MixedStreamOneBytePerRead_WritesWhatTheWholeReadWrites`, `ExecuteAsync_MixedStreamSplitAtEveryOffset_WritesWhatTheWholeReadWrites`, `ExecuteAsync_MixedStreamInRandomPieces_WritesWhatTheWholeReadWrites` (seeded `Random(1519)`, 50 rounds), `ExecuteAsync_NegotiationSplitOneBytePerRead_IsAnsweredOnceThenOffers`, `ExecuteAsync_ServerTogglesEchoAHundredTimes_AnswersEachToggleOnceAndOffersOnce` (negotiation loop stays bounded), `ExecuteAsync_ServerRepeatsDoBinaryAfterAgreeing_IsNotAnsweredAgain`, `ExecuteAsync_OneHandlerRunsManySessionsAtOnce_EachMatchesASessionRunAlone` (32 sessions on one handler), `ExecuteAsync_SameHandlerRunTwiceInARow_StartsTheSecondSessionAfresh`. Time and cancellation are already covered by `TelnetProtocolHandlerMaxTimeTests` and `ExecuteAsync_Cancelled_Throws`.

Defects found: **BL-1665** (High) - `TelnetReceiver` keeps every subnegotiation byte with no limit, so a server sending `IAC SB` without `IAC SE` grows memory without bound; curl caps its buffer at 512 bytes. No test was committed for it (it lands with the fix). No input over 1 MiB, so nothing is `Integration`.

Test count (`dotnet test Curl.Protocol.Telnet.UnitTests --filter "TestCategory!=Integration"`): 229 before (226 passed, 3 skipped off-Windows), 271 after (268 passed, same 3 skipped).

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Telnet handler attacked in 42 new adversarial tests across all four families; SB-without-SE unbounded buffer filed as BL-1665
