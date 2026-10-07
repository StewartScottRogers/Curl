---
id: BL-1505
title: Attack Curl.Protocol.Abstractions.UnitLibrary with adversarial black-box tests in Curl.Protocol.Abstractions.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1471]
touches: [Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1505 — Attack Curl.Protocol.Abstractions.UnitLibrary with adversarial black-box tests in Curl.Protocol.Abstractions.UnitTests

## Goal

`Curl.Protocol.Abstractions.UnitTests` gains adversarial black-box tests that attack `Curl.Protocol.Abstractions.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1471.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Protocol.Abstractions.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: `CurlUrlParser` and its pieces (user info with `@` and `:`, bracketed IPv6 with zone IDs, ports 0, 65535 and 65536, percent-encoding including `%00` and bad hex, IDN, backslashes, dot segments, URLs past 8000 bytes, scheme case), `CurlUrlIPv4Address` short, octal and hex forms, `CurlDateParser` (every date form curl accepts, years 1601, 2038, 9999 and past), Alt-Svc header parsing, and the `IConnection` contract under zero-byte reads, partial reads and cancellation.
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Protocol.Abstractions.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Protocol.Abstractions.UnitTests -warnaserror` is clean and `dotnet test Curl.Protocol.Abstractions.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Protocol.Abstractions.UnitTests/` and this task file.

## Notes

Oracle: real curl 8.21.0 (Schannel mingw build), measured 2026-10-07. URLs through
`curl --proto =dict -w "%{exitcode}|%{url.*}"` (curl parses, then stops with exit 1
before connecting; exit 3 is a rejection, its message pinned as the `CurlUrlRejection`).
Dates through `Record-CurlExchange.ps1` with `-z <text>`, reading the
`If-Modified-Since` curl sent. Dates curl reads but `-z` then refuses with exit 43
(year 1601, 9999, 10000) were not pinned: exit 43 is `-z`'s range check, not the parser's
answer, and years outside `DateTimeOffset` are already pinned by
`TryParse_YearsOutsideDateTimeOffset_AreRead`.

Attack families, all through public members only (no internals, no reflection):

- **Boundaries**: `CurlUrlAdversarialTests.TryParse_WithAPortOnTheEdgeOfItsRange_ReadsThePortCurlReads`
  (0, 65535, empty, 0080), `TryParse_WithAPortOutsideItsRangeOrNotDecimal_RejectsItAsABadPortNumber`
  (65536, 20 digits, -1, +80, `user@:80@host`), `TryParse_WithAShortOctalOrHexIPv4Form_NormalisesItAsCurlDoes`,
  `TryParse_WithAnIPv4FormOutOfRange_KeepsItAsAHostName` (2^32, 1.2.3.256, 1.16777216 ...);
  `CurlDateParserAdversarialTests.TryParse_WithAFieldOnItsLimit_ReadsTheInstantCurlReads`
  (2038-01-19 03:14:07 and :08, second 60, zones +1400 and -1400, two-digit years 69 and 70);
  `ByteRangeAdversarialTests` (`long.MaxValue` and `long.MinValue` on every factory).
- **Malformed input**: `TryParse_WithASpaceInThePortOrPath_RejectsItAsMalformedInput`,
  `TryParse_WithTwoAtSignsInTheAuthority_RejectsTheHostCurlRejects`,
  `TryParse_WithNineGroupsInAnIPv6Literal_RejectsItAsABadIPv6Address`,
  `TryParse_WithABrokenEscapeOrTooManyDotSegmentsInThePath_KeepsThePathCurlKeeps` (`%00`, `%zz`, `%`, `../../..`),
  `TryParse_WithPercentEncodedDelimitersInTheUserInformation_KeepsThemEncoded`,
  `TryParse_WithAZoneIdAndAPort_SplitsTheZoneFromTheHost`, `TryParse_WithAnEmptyUserAndPassword_StillReadsTheHost`;
  `CurlDateParserAdversarialTests.TryParse_WithEveryPrefixOfAValidDate_NeverThrows` and
  `TryParse_WithSeededRandomText_NeverThrows` (seed 1505, 5000 texts, NUL, tab and non-ASCII included).
- **Invalid partitions**: `TryParse_WithASchemeInMixedCase_ReadsTheSchemeInLowerCase` (scheme case);
  `TryParse_WithADayTheMonthDoesNotHave_RollsIntoTheNextMonthAsCurlDoes` (29 Feb 2023, 31 Feb 2024, 31 Apr 2030),
  `TryParse_WithAZoneOrTimeOutsideCurlsForms_IsRefused` (+1401, `20300101 120000`);
  `ByteRangeAdversarialTests.Bounded_WithLongMinValueFirst_ThrowsArgumentOutOfRangeException`,
  `Suffix_WithLongMinValue_ThrowsArgumentOutOfRangeException`.
- **State and concurrency**: `CurlUrlAdversarialTests.TryParse_AfterARejection_StillParsesTheNextUrl`,
  `TryParse_OnManyTasksAtOnce_GivesEachTheAnswerASingleCallGives` (URL and date parsers, 400 parallel calls each),
  `ConnectionNumberSequenceAdversarialTests.NumberNextConnection_OnManyTasksAtOnce_HandsOutEachNumberOnce`
  (10 000 parallel calls, numbers 0 to 9 999 each once).
  The `IConnection` contract (zero-byte and partial reads, cancellation) is not applicable here:
  the library holds only the interface and its default members, which `IConnectionTests` already pins;
  read behaviour belongs to each implementation's own project. Alt-Svc header parsing is not in this
  library (only the `AltSvcHeaderOutcome` record, pinned by `AltSvcHeaderOutcomeTests`), so it is attacked
  where the parser lives. URLs past 8000 bytes and IDN are already pinned by `CurlUrlTests`
  (8 000 000-character limit both sides, IDN refusal).

Defects found: none. Every attack matched real curl, so no follow-up task was filed.

Test count: 730 before, 798 after (68 new test cases), `dotnet test Curl.Protocol.Abstractions.UnitTests --filter "TestCategory!=Integration"`.
No new test reads over 1 MiB, so none is in `Integration`; all use platform-neutral `http` URLs.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. 68 adversarial black-box tests (URL, date, byte range, connection numbering) pinned to measured curl; no defects found
