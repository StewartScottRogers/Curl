---
id: BL-1564
title: Make Curl.Cli.UnitTests' CommandLineInterfaceAndLocalPortOptionTests to CommandLineMailOptionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1564 — Make Curl.Cli.UnitTests' CommandLineInterfaceAndLocalPortOptionTests to CommandLineMailOptionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineInterfaceAndLocalPortOptionTests.cs`, `CommandLineIpAddressFamilyOptionTests.cs`, `CommandLineIpfsGatewayOptionTests.cs`, `CommandLineKeepAliveTimerTests.cs`, `CommandLineLeadingUnicodeWarningTests.cs`, `CommandLineLocationFollowOverrideTests.cs`, `CommandLineMailOptionTests.cs` (76 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineInterfaceAndLocalPortOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineIpAddressFamilyOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineIpfsGatewayOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineKeepAliveTimerTests.|FullyQualifiedName~Curl.Cli.CommandLineLeadingUnicodeWarningTests.|FullyQualifiedName~Curl.Cli.CommandLineLocationFollowOverrideTests.|FullyQualifiedName~Curl.Cli.CommandLineMailOptionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Each class gets a `TestContext` and a private `Parse` that writes `ARRANGE arguments` (every argument quoted) and the parser's `ACT` lines through `CommandLineParseDiagnostics`, as BL-1563 did. The shared `Accept`, `AssertRefused` and `AssertBinding` helpers became instance methods that write the `ASSERT` lines for the tests that use them; every other test writes an `ASSERT` line beside each assertion it makes. `CommandLineLeadingUnicodeWarningTests` also writes `ARRANGE parse as Windows` and the `-K` file's bytes, and the post data's bytes.
- Nothing printed depends on the operating system: `ProcessPlatformParseReadsArgumentsAsUtf8OnlyOffWindows` asserts and prints whether the value matches "off Windows" (always `True`), not the platform's own value.
- Counts in the seven files, before -> after (lines matching): `Assert.` 145 -> 145, `[TestMethod` 76 -> 76, `[DataRow(` 161 -> 161. Every assertion is unchanged; a few now read a value into a local first, and `Parse_LongHostAfterPrefixes_IsNotMalformed`, `Parse_NameOfTheLongestLength_IsNotMalformed` and `Parse_NameLongerThanLibcurlAccepts_IsMarkedMalformed` assert the same `IsMalformed` through a small `AcceptMalformed` helper that writes its `ASSERT` line.
- Filtered detailed run: 207 tests, 205 run and passed, 2 skipped off-platform (`Parse_PastA32BitLongOffWindows_Accepted`, Windows here); 205 `END` lines, none with a zero count. No test printed a `SLOW:` line.
- `dotnet build Curl.Cli.UnitTests -warnaserror` clean; `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"`: 3770 passed, 16 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the seven files writes ARRANGE, ACT and ASSERT diagnostics; build clean, fast tests green.
