---
id: BL-1576
title: Make Curl.Cli.UnitTests' LibcurlSourceCodeTransferOptionTests to UrlEncodedContentTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Cli.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1576 — Make Curl.Cli.UnitTests' LibcurlSourceCodeTransferOptionTests to UrlEncodedContentTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Cli.UnitTests` files writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `LibcurlSourceCodeTransferOptionTests.cs`, `QueryUrlTests.cs`, `UploadFileGlobTests.cs`, `UploadTransferUrlTests.cs`, `UploadUrlTests.cs`, `UrlEncodedContentTests.cs` (64 test methods, counted 2026-10-07).

## Context

- Split from BL-1459 (one per range of files, as BL-1463 and BL-1464 were split); BL-1459 keeps the whole-project checks and depends on this task. Read BL-1459's Context: line format and helper usage are in `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR (ADR-0417); add no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system.
- What matters here: the full argument array (each argument quoted, so spaces and empty arguments show), the parsed option values the test checks, and for a refusal the exit code and the exact stderr line or lines.
- A shared fake or helper in this project may write the lines for the tests that use it, as long as each test's `END` line counts them. Every class in these files is in namespace `Curl.Cli` and named after its file, which the filter below uses.

## Acceptance criteria

- [x] `dotnet test Curl.Cli.UnitTests --filter "FullyQualifiedName~Curl.Cli.LibcurlSourceCodeTransferOptionTests.|FullyQualifiedName~Curl.Cli.QueryUrlTests.|FullyQualifiedName~Curl.Cli.UploadFileGlobTests.|FullyQualifiedName~Curl.Cli.UploadTransferUrlTests.|FullyQualifiedName~Curl.Cli.UploadUrlTests.|FullyQualifiedName~Curl.Cli.UrlEncodedContentTests." --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Cli.UnitTests -warnaserror` is clean and `dotnet test Curl.Cli.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Cli.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- 2026-10-07: The filtered run executes 183 test cases, all passed; every one printed an `END` line and none had a zero arrange, act or assert count. No test printed a `SLOW:` line, so no follow-up task.
- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before = after: LibcurlSourceCodeTransferOptionTests 6/6/56, QueryUrlTests 12/6/21, UploadFileGlobTests 24/10/2, UploadTransferUrlTests 13/8/18, UploadUrlTests 30/30/43, UrlEncodedContentTests 4/4/0.
- Each class's shared act helper writes the lines (BL-1459 Context allows it): `TransferLinesFor` writes the quoted argument array and the setopt lines; `QueryUrlTests.Parse` writes the arguments and the parse result through `CommandLineParseDiagnostics`, and the `--url-query n@missing` refusal goes through `AssertRefusal` (exit 26 and the three stderr lines); `UploadUrlTests.Append`/`IsStandardInput`, `UploadFileGlobTests.Parse`/`ResolveTransferTargets` and `UploadTransferUrlTests.ArrangeUpload`/`ActResolve` write inputs and results; `UrlEncodedContentTests` writes the content as `BYTES`.
- Choice: `TryResolve_FileUrlWithDriveLetter_...` answers differently on Windows and elsewhere by design, so it prints only whether the result is this platform's answer (`true`), keeping the output the same on every platform.
- `dotnet build Curl.Cli.UnitTests -warnaserror` clean, `dotnet format --verify-no-changes` clean; fast tests for the project: 3770 passed, 16 skipped, 0 failed. No library changed, so no coverage run was needed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test from LibcurlSourceCodeTransferOptionTests to UrlEncodedContentTests writes ARRANGE, ACT and ASSERT/DIFF diagnostics
