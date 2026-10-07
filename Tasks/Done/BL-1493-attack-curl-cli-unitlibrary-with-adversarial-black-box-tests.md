---
id: BL-1493
title: Attack Curl.Cli.UnitLibrary with adversarial black-box tests in Curl.Cli.UnitTests
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1491, BL-1443, BL-1459]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-06
completed: 2026-10-07
---
# BL-1493 — Attack Curl.Cli.UnitLibrary with adversarial black-box tests in Curl.Cli.UnitTests

## Goal

`Curl.Cli.UnitTests` gains adversarial black-box tests that attack `Curl.Cli.UnitLibrary`'s public surface at its boundaries, with malformed input, in its invalid input partitions and, where it holds state, under repeated and interleaved calls, by the method in `Documentation/Wiki/Adversarial-Testing.md`; every defect they find is filed as its own task.

## Context

- Stewart's request, 2026-10-06: aggressive black-box testing for every test project, applied only after every other open task that changes that project's tests or library is done. This task depends on each of those open on 2026-10-06 (Backlog, Doing and Blocked; Deferred left out), so it attacks the suite in its final shape: BL-1443, BL-1459.
- The method, rules and oracle are in `Documentation/Wiki/Adversarial-Testing.md` (BL-1491). Black box means `Curl.Cli.UnitLibrary`'s public types and injected fakes only; where the behaviour is visible on the command line the expected answer is real curl's, measured with `Record-CurlExchange.ps1`.
- Where to attack, applying only the families that fit: command-line and config-file parsing: clustered short options, `--`, `--next`, a value-taking option as the last argument, numeric options at and past their limits (overflow, negative, hex, fractions, `k`/`M`/`G` suffixes where curl takes them), `@file` and `@-` data, URL globbing (`[1-100000000]`, `[a-z:0]`, nested and unmatched `{}`/`[]`), `--config` lines with quotes, escapes, a BOM, CR-only line ends and very long lines, unknown options (exit 2).
- `touches` is the test project only. A defect is never fixed here: it becomes a follow-up task, and its test lands with the fix.

## Acceptance criteria

- [x] Notes list each of the four attack families (boundaries, malformed input, invalid partitions, state and concurrency) as applied, with the new test names, or as not applicable, with one line saying why.
- [x] Every new test is in `Curl.Cli.UnitTests`, uses only MSTest and the base class library, touches no real network, and passes on Windows, Linux and macOS (no drive-letter paths or Windows-only text outside an `[OSCondition]` test); tests on inputs over 1 MiB are in `TestCategory("Integration")`.
- [x] Every attack that exposed a defect is filed as a follow-up task (High for a crash, hang, unbounded memory or security issue) whose ID is in Notes; no failing, ignored or inconclusive test is committed for it.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes, and the project's test count is higher than before the task (before and after recorded in Notes).
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.

## Notes

Oracle: curl 8.21.0 (mingw, Schannel), measured 2026-10-07 with `curl -q <args> http://127.0.0.1:1/` and
`CURL_HOME`/`HOME` at an empty directory; config files written with `printf`, values read back with
`-w '%{url_effective}'`. Parser tests pass `isWindows: true` so the Windows `LONG_MAX` answer is pinned on every
platform. New files: `CommandLineParserAdversarialTests.cs`, `UploadFileGlobAdversarialTests.cs`.

- **Boundaries:** `Parse_RetryAtTheIntLimit_IsAccepted`, `Parse_RetryOnePastTheWindowsLongLimitOrNotAnInteger_RefusesAsNotProperNumerical`,
  `Parse_MaxRedirsMinusOne_IsAcceptedAsUnlimited`, `Parse_MaxRedirsMinusTwo_RefusesAsNotProperNumerical`,
  `Parse_LimitRateWithGigabyteSuffix_IsAccepted`, `Parse_ConfigFileLineOfTwentyThousandBytes_KeepsTheWholeValue`,
  `TryParse_SingleValueRange_ExpandsToOneFile` (`[1-1]`, `[0-0]`), `TryParse_RangeOfAHundredMillion_CountsWithoutExpandingAndYieldsTheFirstLazily`.
- **Malformed input:** `Parse_ValueTakingOptionAsTheLastArgument_RefusesAsRequiresParameter`, `Parse_ClusterWithAnUnknownLetter_RefusesNamingTheWholeCluster`,
  `Parse_MalformedOrUnknownOptionName_RefusesAsUnknown` (`-`, `---x`, `--bogus`), `Parse_OptionNameWithALoneSurrogateOrANul_RefusesAsUnknownWithoutThrowing`,
  `Parse_NumericLongOptionWithEqualsAndNoValue_RefusesNamingTheOptionAsSpelled`, `Parse_OutputWithEqualsAndNoValue_RefusesAsBlankArgument`,
  `Parse_LongOptionJoinedToItsValueWithEquals_TakesTheValue`, `Parse_OptionAfterDoubleDash_IsTakenAsAUrl`, `Parse_DoubleDashAlone_RefusesAsNoUrl`,
  `Parse_ConfigFileUnterminatedQuote_TakesTheRestOfTheLine`, `Parse_ConfigFileQuotedValue_DecodesEveryEscapeAndDropsTheBackslashBeforeAnyOther`,
  `Parse_ConfigFileTextAfterTheClosingQuote_IsIgnoredSilently`, `Parse_ConfigFileStartingWithAByteOrderMark_RefusesTheFirstOptionAsUnknown`,
  `Parse_ConfigFileWithCarriageReturnOnlyLineEnds_ReadsOneLineWithTheCarriageReturnInTheOptionName`,
  `Parse_ConfigFileUrlWithNothingAfterTheEquals_RefusesAsRequiresParameter`, `Parse_ConfigFileOfOnlyBlankAndTabLines_AddsNothing`,
  `TryParse_MalformedGlob_RefusesAsUrlMalformatWithCurlsMessage` (10 globs: `[a-z:0]`, `[1-3:0]`, `[1-`, overflowing range, `[-1-3]`, unmatched, nested and empty braces, stray `]` and `}`).
- **Invalid partitions:** `Parse_RetryNegative_RefusesAsNotPositiveNumerical`, `Parse_MaxTimeNotANonNegativeNumber_RefusesAsNotProperNumerical`,
  `Parse_MaxTimeFraction_IsAccepted`, `Parse_SizeWithAnUnknownSuffix_RefusesAsBadlyUsed`, `Parse_LimitRateNegative_RefusesAsNotProperNumerical`,
  `Parse_ProtoWithOnlyAnUnknownProtocolAfterEquals_WarnsAndRefusesAsBadlyUsed`, `Parse_NoPrefixOnANumericOption_RefusesAsNotReversible`,
  `Parse_NextBeforeAnyUrl_RefusesAsMissingUrlThenBadlyUsed`, `Parse_ConfigFileNumericOptionNotANumber_RefusesNamingTheFileLineAndOption`.
- **State and concurrency:** `Parse_SameRefusedArgumentsTwiceThenValidOnes_AnswersEachCallAfresh`,
  `Parse_ManyCommandLinesOnManyTasksAtOnce_AnswerAsTheyDoOneAfterAnother` (200 seeded command lines, seed 1493,
  run on many tasks against the sequential answers). Time and cancellation do not apply: the parser takes no
  `TimeProvider` or token.
- **Defects found:** none; every attack matched curl, so no follow-up task was filed.
- `@file`/`@-` data and `--next` grouping are already attacked by `CommandLineDataFileTests` and
  `CommandLineNextGroupTests`; not repeated. Over-1 MiB inputs: none used, so no Integration test.
- Test count (`dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"`): 3786 before, 3843 after.
  No library changed, so `Measure-CodeQuality.ps1` was not run.

## Log

- 2026-10-06: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Curl.Cli.UnitTests attacks the parser, config files and upload globs with 57 adversarial tests pinned to curl 8.21.0; no defects found
