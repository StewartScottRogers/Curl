---
id: BL-1496
title: Attack Curl.Cookies.UnitLibrary with adversarial black-box tests in Curl.Cookies.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1462]
touches: [Curl.Cookies.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1496 — Attack Curl.Cookies.UnitLibrary with adversarial black-box tests in Curl.Cookies.UnitTests

## Goal

`Curl.Cookies.UnitTests` gains adversarial black-box tests that attack `Curl.Cookies.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1462.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Cookies.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: `Set-Cookie` and cookie-jar parsing: `Domain` with a leading dot, a public suffix, an IP address or a different case; `Path` edge forms; `Expires` far past, far future, malformed and with two-digit years; `Max-Age` of 0, negative and past `long.MaxValue`; name and value at and past curl's 4096-byte limits and its per-domain count limit; Netscape jar lines with too few tabs, `#HttpOnly_` prefixes and comments; prefix rules for `__Secure-` and `__Host-`.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Cookies.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Cookies.UnitTests -warnaserror` is clean and `dotnet test Curl.Cookies.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Cookies.UnitTests/` and this task file.

## Notes

- All new tests are in `Curl.Cookies.UnitTests/CookieAdversarialTests.cs`, one class for the whole library's
  attack surface (the first adversarial task; the class name says it attacks the cookie library rather than
  one production type). Oracle: each member's documented contract, which the existing tests pinned to
  curl 8.21.0 measurements; no new curl behaviour was pinned, so `Record-CurlExchange.ps1` was not needed.
- Test count: 371 before (370 passed, 1 skipped off-Windows), 516 after (515 passed, 1 skipped).
- **Boundaries** (applied): `Parse_NameAndValueAroundLongestNameAndValue_KeepsAtTheLimitAndRefusesOnePast`,
  `Parse_HeaderAroundLongestHeaderValue_ReadsAtTheLimitAndDropsOnePastSilently`,
  `Parse_MaxAgeAtEveryBoundary_ExpiresAsDocumented` (0, negative, `+`, `long.MaxValue`, one past it, 38 digits,
  exactly 400 days and one second past),
  `Parse_MaxAgeOfLongMaxValueReceivedAtTheLastRepresentableSecond_CapsWithoutOverflowing`,
  `Parse_ExpiresFarPastFarFutureAndMalformed_ExpiresAsDocumented`, `Parse_ExpiresWithTwoDigitYear_ReadsTheYearAsCurlDoes`,
  `Parse_ExpiresAroundEightyCharacters_ReadsShorterAndIgnoresAtEighty`,
  `StoreFromResponse_HeadersAroundMostCookiesStoredPerResponse_StoresNoMoreThanTheLimit`,
  `StoreFromResponse_StoredCountAtEveryBoundary_ReturnsTheNextCountWithoutOverflow`,
  `GetCookieHeader_CookiesAroundMostCookiesSent_SendsNoMoreThanTheLimitAndSaysSoAtIt`,
  `GetCookieHeader_HeaderAroundLongestCookieHeader_SendsAtTheLimitAndLeavesOutOnePast`,
  `Read_LineAroundLongestLine_ReadsAtTheLimitAndStopsReadingOnePast`,
  `Read_StreamedLineOfTwoMebibytesWithNoLineFeed_StopsReadingWithoutThrowing` (streamed by a generating
  `TextReader`, so it stays in the fast run as the method allows; no input over 1 MiB is held in memory by the
  test, so no Integration test was needed - and none could live in this project),
  `ParseLine_ExpiryFieldAtEveryBoundary_KeepsTheLeadingDigitsUncapped`,
  `ParseLine_ExpiryFieldPastLongOrWithoutDigits_RefusesTheLine`. curl has no per-domain count limit to attack;
  the per-response (50) and per-request (150) limits stand in for it.
- **Malformed input** (applied): `Parse_HeaderWithNoUsableFirstPart_RefusesAsInvalidCookie`,
  `Parse_ControlCharactersInjected_RefusesWithTheOctetsLine` (CR LF, bare LF, NUL, DEL and tab injected into
  names, values, `Path` and `Domain`), `Parse_SeededRandomHeaders_NeverThrowAndKeepOnlyCookiesThatHonourTheContract`
  (seed 1496, 3000 cases through `Parse`, `ParseFromCookieFile` and `NetscapeCookieFile.ParseLine`),
  `ParseLine_MalformedJarLine_RefusesWithoutThrowing` (too few and too many tabs, empty, tabs only, a bare and a
  wrong-case `#HttpOnly_`, comments, a leading CR, control characters), `ParseLine_SixFields_ReadsAnEmptyValue`,
  `ParseLine_CarriageReturnInsideTheValue_EndsTheLineThere`, `ParseLine_HttpOnlyPrefix_KeepsTheCookieAsHttpOnlyAndWritesItBack`,
  `WriteAndRead_SeededRandomJarLines_ReadBackTheSameCookies` (seed 1496, 500 generated jar lines round-tripped).
- **Invalid partitions** (applied): `Parse_DomainInEveryPartition_KeepsOnlyTheHostOrAParent` (leading dot, two
  dots, a bare dot, a different case, a suffix that is not a label boundary, a child of the host, IPv4 and IPv6
  hosts), `Parse_DomainOfAnIpAddress_StaysHostOnly`, `GetCookieHeader_DomainSetInADifferentCase_IsSentToTheHost`,
  `StoreFromResponse_DomainThatIsAPublicSuffix_DropsTheCookieAndSaysWhy`,
  `StoreFromResponse_DomainThatIsTheHostOrLongerThanItsPublicSuffix_KeepsTheCookie`,
  `Parse_NamePrefixInEveryPartition_KeepsOnlyCookiesThatSatisfyIt` (`__Secure-` and `__Host-` in each invalid
  zone, and their lower-case forms, which carry no rule), `ParseLine_NamePrefixOnAJarLine_KeepsOnlyLinesThatSatisfyIt`,
  `Parse_SecureFromAnOriginThatIsNotSecure_RefusesTheCookie`, `Parse_PathEdgeForms_SanitizesAsDocumented`.
- **State and concurrency** (applied): `StoreFromResponse_AfterARefusedHeader_StoresTheNextOneUnaffected`,
  `StoreFromResponse_SameCookieManyTimes_KeepsOneAndReportsReplaced`,
  `GetCookieHeader_ClockMovingBackwardsAndForwards_NeverResurrectsAnExpiredCookie`,
  `LoadCookieFile_SameFileTwice_KeepsOneCopyOfEachCookie`,
  `EveryMember_ManyConcurrentCallers_EndWithTheSameCookiesAsOneAfterAnother` (64 callers each loading a file,
  storing a response, building a header and writing the jar against one store, compared with the same calls
  one after another). Cancellation is not attacked: the synchronous members take no token, and the two async file
  members only pass theirs to `IFileSystem` and the stream, whose .NET convention (`OperationCanceledException`)
  is all their doc comments imply; their failure paths are already covered by `CookieStoreTests.CookieFiles`.
- Defects found: none. Every attack held, so no follow-up task was filed.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. 145 adversarial tests added across the four families; no defects found
