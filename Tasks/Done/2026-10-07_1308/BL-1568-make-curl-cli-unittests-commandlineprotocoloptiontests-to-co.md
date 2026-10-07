---
id: BL-1568
title: Make Curl.Cli.UnitTests' CommandLineProtocolOptionTests to CommandLineRangeOptionTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1568 — Make Curl.Cli.UnitTests' CommandLineProtocolOptionTests to CommandLineRangeOptionTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CommandLineProtocolOptionTests.cs`, `CommandLineProtocolSetOptionTests.cs`, `CommandLineProxyTests.cs`, `CommandLineProxyTlsOptionTests.cs`, `CommandLineProxyTlsSrpOptionTests.cs`, `CommandLineProxyVariantOptionTests.cs`, `CommandLineQualityOfServiceTests.cs`, `CommandLineRangeOptionTests.cs` (91 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.CommandLineProtocolOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineProtocolSetOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineProxyTests.|FullyQualifiedName~Curl.Cli.CommandLineProxyTlsOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineProxyTlsSrpOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineProxyVariantOptionTests.|FullyQualifiedName~Curl.Cli.CommandLineQualityOfServiceTests.|FullyQualifiedName~Curl.Cli.CommandLineRangeOptionTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Each class got a `TestContext` and an instance `Parse` helper (as BL-1567 did) that writes `ARRANGE arguments` (each argument quoted, through `CommandLineParseDiagnostics`) and the parse result as `ACT` lines (accepted, exit code, every stderr and warning line). The OpenSSL-build classes also write `ARRANGE build: OpenSSL`, and `CommandLineProxyTlsOptionTests` which path-exists function it used. Each test then writes an `ASSERT` (or `DIFF`) for every value it checks: refusals their exit code and full stderr lines, accepted parses the option values (text quoted, so empty and null differ; post data as `BYTES` and a `DIFF`; scheme sets sorted). `CommandLineProxyTests` (not a parser test) writes the address, the kind without a scheme, and the `TryGetKind` result and failure; the AI-help and null-argument tests write their inputs and results.
- Where a test reads `result.Options`, its `Assert.IsTrue(result.IsAccepted)` now runs before the diagnostic lines (the compiler needs it for nullability); no assertion was changed or removed, only that one line moved up.
- Counts (`Assert.`, `[TestMethod`, `[DataRow(`), before -> after: CommandLineProtocolOptionTests 35/16/26 -> 35/16/26; CommandLineProtocolSetOptionTests 38/15/40 -> 38/15/40; CommandLineProxyTests 14/4/21 -> 14/4/21; CommandLineProxyTlsOptionTests 68/12/27 -> 68/12/27; CommandLineProxyTlsSrpOptionTests 20/7/11 -> 20/7/11; CommandLineProxyVariantOptionTests 29/11/32 -> 29/11/32; CommandLineQualityOfServiceTests 15/6/51 -> 15/6/51; CommandLineRangeOptionTests 55/20/62 -> 55/20/62.
- The acceptance filter runs 310 tests on Windows (308 run, 2 skipped by `OSCondition`): 308 `END` lines, none with a zero arrange, act or assert count. The whole fast run of Curl.Cli.UnitTests: 3770 passed, 16 skipped, 0 failed.
- No test printed a `SLOW:` line.
- Only test code changed, so no library's coverage moved; Measure-CodeQuality.ps1 was not run.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in CommandLineProtocolOptionTests to CommandLineRangeOptionTests writes ARRANGE, ACT and ASSERT diagnostics
