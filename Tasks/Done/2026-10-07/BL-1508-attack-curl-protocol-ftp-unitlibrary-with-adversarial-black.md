---
id: BL-1508
title: Attack Curl.Protocol.Ftp.UnitLibrary with adversarial black-box tests in Curl.Protocol.Ftp.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1474]
touches: [Curl.Protocol.Ftp.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1508 — Attack Curl.Protocol.Ftp.UnitLibrary with adversarial black-box tests in Curl.Protocol.Ftp.UnitTests

## Goal

`Curl.Protocol.Ftp.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Ftp.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1474.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Ftp.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: reply parsing (multi-line `123-` forms, a missing or out-of-range code, an unterminated reply), `PASV` and `EPSV` replies with port 0, a port past 65535 and a foreign address, `LIST` and `SIZE` replies that lie, path segments carrying `%0D%0A` command injection, `--ftp-method` variants and resume offsets at 0, the file size and past it.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Ftp.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Ftp.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Ftp.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Ftp.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Protocol.Ftp.UnitTests/FtpProtocolHandlerAdversarialTests.cs`, driven through `FtpProtocolHandler` (the library's only public type) with `ScriptedConnection` and a connector fake; no input is over 1 MiB (the largest is a 64 KiB reply line), so none is Integration. The oracle is curl 8.21.0 (Schannel), measured 2026-10-07 with `Record-CurlExchange.ps1 -Ftp` for the greeting codes 099/150/600, a tab after the code, the fake-last-line multi-line greeting, `%0D%0A` and `%00` in the path, `SIZE` replies `abc`/`-5`/23 digits/9-for-5-bytes, `-C 9223372036854775807`, EPSV `(|||-1|)`, 26 digits and an unclosed port, PASV `0,0` and `0001`, and `RETR` answered `226`; the handler's doc comments elsewhere (65535-byte line limit, cancellation leaving as `OperationCanceledException`).

- **Boundaries:** `ExecuteAsync_GreetingCodeOutsideTwoHundreds_FailsWithExit8AndSendsNothing` (199, 300, 600, 999), `ExecuteAsync_EpsvPortAtTheEdgesOfItsRange_DialsThatPort` (1, 65535), `ExecuteAsync_EpsvPortPast65535_FailsWithExit13IllegalPortAndDialsNothing`, `ExecuteAsync_PasvPortAtTheEdgesOfItsRange_DialsThatPortOnTheControlHost`, `ExecuteAsync_PasvNumbersOutOfRangeOrMissing_FailsWithExit14`, `ExecuteAsync_ReplyLineOf65534BytesBeforeItsLineFeed_IsRead`, `ExecuteAsync_ReplyLineOf65535BytesBeforeItsLineFeed_FailsWithExit100`, `ExecuteAsync_ResumeFromOneByteBeforeTheEnd_SendsRestAndWritesTheLastByte`, `ExecuteAsync_ResumeFromLongMaxValue_FailsWithExit36BeyondTheSize`. EPSV port 0 and resume at 0 and at the size were already pinned (`FtpProtocolHandlerEpsvDialFallbackTests`, `FtpProtocolHandlerRangeTests`), so not repeated.
- **Malformed input:** `ExecuteAsync_MultiLineGreetingWithFakeLastLinesInside_WaitsForTheCodeAndSpace`, `ExecuteAsync_LastLineEndedByBareLineFeeds_IsReadLikeCrLf`, `ExecuteAsync_GreetingCutOffBeforeItsLastLineEnds_FailsWithExit56AndSendsNothing`, `ExecuteAsync_GreetingWhoseCodeIsNotThreeDigitsAndASpace_IsNeverALastLine`, `ExecuteAsync_NulByteInsideAMultiLineReply_FailsWithExit8`, `ExecuteAsync_EpsvReplyMalformed_FailsWithExit13AndDialsNothing`, `ExecuteAsync_PasvNamingAForeignAddress_DialsTheControlHostByDefault`, `ExecuteAsync_SizeReplyThatIsNotANumber_DownloadsEveryByteSent`, `ExecuteAsync_SizeReplyLargerThanTheData_FailsWithExit18NamingTheBytesMissing`.
- **Invalid partitions:** `ExecuteAsync_PathCarryingAControlCharacter_FailsWithExit3BeforeAnyCommandCarriesIt` (CR LF injection, bare CR, bare LF, NUL, in a directory segment), `ExecuteAsync_EveryFileMethodWithAnInjectedLineEnd_FailsWithExit3` (multicwd, singlecwd, nocwd), `ExecuteAsync_RetrAnsweredWithACompletionBefore150_FailsWithExit19` (226 before 150, 250, 200), `ExecuteAsync_GreetingThatIsAnotherStatesReply_FailsWithExit8` (150, 331).
- **State and concurrency:** `ExecuteAsync_EveryReplyDeliveredOneBytePerRead_SendsTheSameCommandsAndBody`, `ExecuteAsync_ControlClosedBetweenTheTwoLinesOfAMultiLineReply_FailsWithExit56`, `ExecuteAsync_SameHandlerRunTwice_GivesTheSameCommandsAndBodyBothTimes`, `ExecuteAsync_SixteenTransfersAtOnceOnOneHandler_EachWritesItsOwnBody`, `ExecuteAsync_TokenCancelledBeforeTheCall_ThrowsOperationCanceledAndWritesNothing`. Time attacks are not repeated: `FtpProtocolHandlerTimeLimitTests` already drives the injected `TimeProvider` past the connect and transfer limits.

Defects found, filed, and left without a test here (their tests land with the fixes):
- BL-1659 (Normal): greeting `099` gives `Got a 99 ftp-server response...`; curl prints `Got a 099`.
- BL-1660 (Normal): a `227` naming port 0, or a number with leading zeros past three digits (`0001`), fails with exit 14; curl dials port 0 (exit 7) and reads `0001` as 1.

Test count: 616 before (612 passed, 4 skipped), 681 after (677 passed, 4 skipped). `Measure-CodeQuality.ps1` was not run: the task adds tests only and changes no library code, so the library's coverage cannot fall.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. FtpProtocolHandler attacked by 65 adversarial black-box tests across all four families; two defects filed as BL-1659 and BL-1660
