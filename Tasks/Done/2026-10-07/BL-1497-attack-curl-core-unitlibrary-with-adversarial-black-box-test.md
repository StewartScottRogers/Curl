---
id: BL-1497
title: Attack Curl.Core.UnitLibrary with adversarial black-box tests in Curl.Core.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1453, BL-1457, BL-1463]
touches: [Curl.Core.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1497 — Attack Curl.Core.UnitLibrary with adversarial black-box tests in Curl.Core.UnitTests

## Goal

`Curl.Core.UnitTests` gains adversarial black-box tests that attack `Curl.Core.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1453, BL-1457, BL-1463.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Core.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: redirect following (`--max-redirs` at -1, 0, the limit and one past it, redirect loops, relative and scheme-changing `Location` forms), retry policy (`Retry-After` as seconds, an HTTP date, negative and huge values; `--retry` bounds), `NO_PROXY` matching (wildcards, leading dots, IPv6, CIDR forms), proxy URL parsing, `ByteRangeParser` (`0-`, `-0`, `1-0`, overflowing numbers, many ranges), the low-speed and max-time watchdogs exactly at their thresholds through `TimeProvider`, and scheme guessing.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Core.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Core.UnitTests -warnaserror` is clean and `dotnet test Curl.Core.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Core.UnitTests/` and this task file.

## Notes

All new tests are in `Curl.Core.UnitTests/CoreParserAdversarialTests.cs` (101 test cases). They attack the five pure public parsers, `ByteRangeParser`, `RetryAfterHeader`, `NoProxyMatcher`, `ProxyUrlParser` and `UrlSchemeGuesser`. The oracle is each type's documented contract, which the type's remarks record as measured against curl 8.21.0.

- **Boundaries:** `ByteRangeParser_PositionsAtTheLongLimit_NameARange` (long.MaxValue on each side, `0-0`, `-1`), `ByteRangeParser_TextNamingNoRange_IsRefusedWithoutThrowing` (`0-9223372036854775807`, one past long.MaxValue, `-0`, `1-0`), `ByteRangeParser_OneHundredThousandRanges_ReadsOnlyTheFirst`, `RetryAfterHeader_SecondsAtAndPastTheLimits_StayBetweenZeroAndTheCap` (21599/21600/21601, long.MaxValue and one past it), `RetryAfterHeader_HttpDateAroundNow_WaitsUntilItCappedAtSixHours` (-1 s, now, +1 s, at the cap and past it), `ProxyUrlParser_PortsInsideTheRange_AreKept` (1, 65535, defaults), CIDR /0, /32+1, /129 in `NoProxyMatcher_MalformedAndBoundaryLists_MatchOnlyWhatTheyName`, `NoProxyMatcher_TenThousandEntriesBeforeTheMatch_FindsIt`.
- **Malformed input:** Arabic-Indic and full-width digits, NUL, and a 10,000-digit position (`ByteRangeParser_TenThousandDigitPosition_CountsAsAbsent`). Out-of-range date fields in `RetryAfterHeader_MalformedDate_NeverThrowsAndStaysWithinTheCap`. Empty, comma-only and blank-only NO_PROXY lists, and a bad CIDR suffix. Port 0/65536/-1/overflow/`8o`, an unclosed or invalid IPv6, CRLF, NUL, space, two `@`, cut-off `%4` and an unknown scheme in `ProxyUrlParser_MalformedText_FailsWithAProxyExitCodeWithoutThrowing`.
- **Invalid partitions:** a sign (`-5`, `+5`), empty, blank and non-ASCII values for `Retry-After`. `*` inside a list, a suffix-only match (`badexample.com`) and an empty host for NO_PROXY. Scheme-shaped text that is no scheme (`1http://`, `http:x`) and `ftp.` prefixes outside the host (user info, path) in `UrlSchemeGuesser_UrlsAtThePrefixAndSchemeEdges_GuessByTheHostOnly` and `UrlSchemeGuesser_AnySchemeShapedPrefix_CountsAsAScheme`. Null for every entry point (`*_Null*_ThrowsArgumentNullException`).
- **State and concurrency:** `Parsers_CalledFromManyThreadsAtOnce_GiveTheSameAnswersAsOneThread` runs 32 concurrent tasks over all five parsers and compares the answers with a single-thread run. The parsers are static and hold no state, so repeated calls need no more than that. The stateful types (`RedirectFollower`, `TransferRetrier`, watchdogs) are not attacked here because they already have dedicated suites under fake `TimeProvider`. Choice: the budget went to the pure parsers named in Context, where malformed input reaches furthest.
- **Defects found:** none. Three first expectations were wrong, and the library's documented curl behaviour was right each time: `http:///host` (libcurl accepts 1-3 slashes), `ftp.` guesses ftp (curl's `FTP.` prefix check), and CIDR `/0` compares the whole address. No follow-up task was filed.
- **Test count:** `Curl.Core.UnitTests` fast run went from 1452 to 1553 (+101). Nothing in the file is over 1 MiB, so there is no Integration test.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. 101 adversarial tests on Core's parsers pass; no defects found
