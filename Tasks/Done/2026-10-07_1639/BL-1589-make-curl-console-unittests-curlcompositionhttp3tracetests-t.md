---
id: BL-1589
title: Make Curl.Console.UnitTests' CurlCompositionHttp3TraceTests to CurlCompositionProxyTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1589 — Make Curl.Console.UnitTests' CurlCompositionHttp3TraceTests to CurlCompositionProxyTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (13 files, 98 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCompositionHttp3TraceTests.cs`, `CurlCompositionHttpProxyTraceTests.cs`, `CurlCompositionHttpTests.cs`, `CurlCompositionImapTests.cs`, `CurlCompositionLdapTests.cs`, `CurlCompositionLocalBindingTests.cs`, `CurlCompositionNegotiateTests.cs`, `CurlCompositionNtlmTests.cs`, `CurlCompositionPlatformLibraryTests.cs`, `CurlCompositionPop3Tests.cs`, `CurlCompositionProxyTests.ConnectHeaders.cs`, `CurlCompositionProxyTests.cs`, `CurlCompositionProxyTests.Http2Tunnel.cs`.

## Context

- Split from BL-1461 (one task per range of files, as its Notes direct); BL-1461 keeps the whole-project checks and depends on this task. Follow BL-1461's Context: what matters in this project (command line, the scripted connector's script, request bytes, stdout and stderr, exit code), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1541 and BL-1542 show the pattern in `Curl.Networking.UnitTests`.
- "These classes' filter" below is one `FullyQualifiedName~Curl.Console.<Class>.` term per class the listed files declare (a partial class's files count once), joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Console.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Console.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Counts in the 13 files, before -> after: `Assert.` 270 -> 270, `[TestMethod` 98 -> 98, `[DataRow(` 102 -> 102. No test's logic or assertions changed; `Diagnostics.Assert(` lines are additions.
- The filtered detailed run printed 168 `END` lines for 174 tests (6 skipped by `OSCondition` on Windows), none with a zero count.
- Shared lines go through a new `CurlRunDiagnostics.cs` in the project (command line as Arrange; exit code, stdout, stderr and request `BYTES` as Act; request `DIFF`), so each runner helper writes them once. Chosen over repeating the lines in every file.
- Nothing printed depends on the operating system: stdout and stderr are written with `\n` line endings, stderr asserts compare `\n`-normalised text, the IMAP upload's temporary path is written as its file name, and the platform code page test writes only whether the encoding is the platform's.
- No test printed a `SLOW:` line.

- Counts in the 13 files, before -> after: `Assert.` 270 -> 270, `[TestMethod` 98 -> 98, `[DataRow(` 102 -> 102. No test's logic or assertions changed; `Diagnostics.Assert(` lines are additions.
- The filtered detailed run printed 168 `END` lines for 174 tests (6 skipped by `OSCondition` on Windows), none with a zero count.
- Shared lines go through a new `CurlRunDiagnostics.cs` in the project (command line as Arrange; exit code, stdout, stderr and request `BYTES` as Act; request `DIFF`), so each runner helper writes them once. Chosen over repeating the lines in every file.
- Nothing printed depends on the operating system: stdout and stderr are written with `\n` line endings, stderr asserts compare `\n`-normalised text, the IMAP upload's temporary path is written as its file name, and the platform code page test writes only whether the encoding is the platform's.
- No test printed a `SLOW:` line.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the 13 files writes Arrange, Act and Assert diagnostics; build clean, fast tests green
