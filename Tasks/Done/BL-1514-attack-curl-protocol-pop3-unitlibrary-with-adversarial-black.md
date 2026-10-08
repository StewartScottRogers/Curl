---
id: BL-1514
title: Attack Curl.Protocol.Pop3.UnitLibrary with adversarial black-box tests in Curl.Protocol.Pop3.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1480]
touches: [Curl.Protocol.Pop3.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1514 — Attack Curl.Protocol.Pop3.UnitLibrary with adversarial black-box tests in Curl.Protocol.Pop3.UnitTests

## Goal

`Curl.Protocol.Pop3.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Pop3.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1480.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Pop3.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: multi-line replies with dot-stuffing and a missing terminator, `+OK` and `-ERR` variants, message numbers 0, negative and past `int.MaxValue`, APOP timestamps that are malformed, and CRLF injection through the URL path and `-X`.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Pop3.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Pop3.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Pop3.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Pop3.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Protocol.Pop3.UnitTests/Pop3ProtocolHandlerAdversarialTests.cs`, driving the public `Pop3ProtocolHandler` through `ScriptedConnection` only. Real curl 8.21.0 (Schannel) measured on 2026-10-07 with `Record-CurlExchange.ps1 -Pop3 -Pop3Reply ...`.

- **Boundaries**: `ExecuteAsync_MessageNumberAtOrPastAnIntegerBoundary_IsSentVerbatimAndItsErrIsExit8` (0, -1, int.MaxValue, int.MaxValue + 1, 99999999999999999999; measured: sent verbatim, -ERR is exit 8 with QUIT); `ExecuteAsync_DotStuffedBodyCutAtEveryByte_WritesTheSameUnstuffedMessage` (the dot-stuffed `RETR` answer split into two reads at every one of its byte positions); `ExecuteAsync_ServerHangsUpInsideTheTerminator_SucceedsWritingAPrefixOfTheBodyWithoutQuit` (hang-up after `.`, `.\r`, a lone CR, a stuffed `..`; the recorder always appends CRLF so these cannot be measured, so the oracle is: the run ends, sends no QUIT, writes only bytes the server sent).
- **Malformed input**: `ExecuteAsync_StatusLineOkFollowedByLetters_CountsAsOkAndWritesTheBody` (`+OKAY`, measured exit 0); `ExecuteAsync_StatusLineNearlyOkOrErr_FailsWithExit8AndStillQuits` (`+ok`, `-ERRx`, measured exit 8); `ExecuteAsync_LowerCaseErrLine_IsNotAStatusLineSoTheHangUpIsExit56WithoutQuit` (`-err no`, measured exit 56); `ExecuteAsync_MalformedApopTimestamp_LogsInAsCurlDoes` (`<@>`, `+OK<a@b>`, `<nohost>`, `<a@b`, `a@b>`, each digest measured).
- **Invalid partitions**: `ExecuteAsync_CrlfSmuggledThroughTheUrlPath_SendsNoSecondCommandAndFailsWithExit3` (`%0d%0a`, `%0a` in the path) and `ExecuteAsync_CrlfSmuggledThroughTheCustomCommand_SendsNoSecondCommandAndFailsWithExit3` (raw and escaped CRLF/LF in `-X`): no second command reaches the wire, exit 3, as the existing measured control-character cases.
- **State and concurrency**: `ExecuteAsync_OneHandlerRunTwiceInARow_GivesTheSecondRunTheSameAnswerAsTheFirst` and `ExecuteAsync_OneHandlerOnSixteenTasksAtOnce_GivesEveryTransferItsOwnMessage` (one handler instance, 16 concurrent transfers on their own connections).
- **Defect found**: BL-1662 (High) - the APOP timestamp is taken only when the greeting ends in `>`, and then runs to the end of the line; curl takes `<` to the first `>`. With `+OK hi <a@b> trailing` Curl sends `USER`/`PASS` (the password in the clear) where curl sends `APOP`; with `+OK <a@b> <c@d>` it sends the wrong digest. Those three greetings are not pinned here (no failing test committed); their tests land with the fix.
- Test count: 317 before (312 passed, 5 skipped), 345 after (340 passed, 5 skipped). No input over 1 MiB, so no Integration tests.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. 28 adversarial tests added (317 -> 345); APOP timestamp defect filed as BL-1662
