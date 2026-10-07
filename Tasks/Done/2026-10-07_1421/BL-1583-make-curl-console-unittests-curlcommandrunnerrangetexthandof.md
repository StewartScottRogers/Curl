---
id: BL-1583
title: Make Curl.Console.UnitTests' CurlCommandRunnerRangeTextHandOffTests to CurlCommandRunnerRemoteTimeTests tests write descriptive diagnostic output
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-1457]
touches: [Curl.Console.UnitTests]
requirement: none
created: 2026-10-07
completed: 2026-10-07
---
# BL-1583 — Make Curl.Console.UnitTests' CurlCommandRunnerRangeTextHandOffTests to CurlCommandRunnerRemoteTimeTests tests write descriptive diagnostic output

## Goal

Every test in these `Curl.Console.UnitTests` files (6 files, 90 test methods, counted 2026-10-07) writes, through BL-1457's `TestDiagnostics` helper, its Arrange inputs, Act result and assertion context (plus `PHASE` timings where it has phases), with no test's logic or assertions changed: `CurlCommandRunnerRangeTextHandOffTests.cs`, `CurlCommandRunnerRateTests.cs`, `CurlCommandRunnerRedirectProgressMeterTests.cs`, `CurlCommandRunnerRedirectTests.cs`, `CurlCommandRunnerRemoteNameTests.cs`, `CurlCommandRunnerRemoteTimeTests.cs`.

## Context

- Split from BL-1461 (one task per range of files, as its Notes direct); BL-1461 keeps the whole-project checks and depends on this task. Follow BL-1461's Context: what matters in this project (command line, the scripted connector's script, request bytes, stdout and stderr, exit code), `Documentation/Wiki/Test-Diagnostics.md` and BL-1457's ADR for the line format; no prefix of your own; output only; large payloads through `BYTES`; nothing printed may depend on the operating system. BL-1541 and BL-1542 show the pattern in `Curl.Networking.UnitTests`.
- `CurlCommandRunnerRangeTextHandOffTests.cs` already writes output of its own; move it onto the helper's format (BL-1461's Context).
- "These classes' filter" below is one `FullyQualifiedName~Curl.Console.<Class>.` term per class the listed files declare (a partial class's files count once), joined with `|`.

## Acceptance criteria

- [x] `dotnet test Curl.Console.UnitTests --filter "<these classes' filter>" --logger "console;verbosity=detailed"` prints an `END` line for every test it runs, and none matches `END .*(\(arrange 0,|, act 0,|, assert 0\))`.
- [x] In these files the numbers of `Assert.`, `[TestMethod` and `[DataRow(` matches are no lower than before; before and after numbers are in Notes.
- [x] `dotnet build Curl.Console.UnitTests -warnaserror` is clean and `dotnet test Curl.Console.UnitTests --filter "TestCategory!=Integration"` passes.
- [x] The task's commits change only files under `Curl.Console.UnitTests/` and this task file.
- [x] Notes list every test that printed a `SLOW:` line with its `PHASE` breakdown, or say none did; a real performance problem gets a follow-up task whose ID is in Notes.

## Notes

- Pattern as BL-1582: each class's `RunAsync` writes ARRANGE (arguments, scripted responses or handler, platform flags), wraps the run in `PHASE run`, and writes ACT (exit code, stdout, stderr, plus requests, files, stamps, start times or waits where the class checks them); each test writes ASSERT/DIFF lines for what it asserts, before its unchanged `Assert` calls. Text is printed LF-normalised.
- `CurlCommandRunnerRangeTextHandOffTests` printed its temporary directory with `TestContext.WriteLine`; that line is now `ARRANGE temporary file`, and every temporary path or file URL in arguments, stdout and stderr prints as `<temporary path>` (the temp root holds a space on this machine, so it is replaced first, then the `curl-bl1322-<guid>` part), so the log reads the same on every OS.
- `RemoteName`'s last test runs the runner without `outputPaths`, so it writes its own ARRANGE/PHASE/ACT lines.
- Filter run 2026-10-07: 96 tests, 96 `END` lines, none with a zero count.
- Counts (`Assert.` / `[TestMethod` / `[DataRow(`), before = after: RangeTextHandOff 22/8/2, Rate 25/13/0, RedirectProgressMeter 6/3/0, Redirect 34/11/6, RemoteName 84/33/0, RemoteTime 61/22/4.
- No test printed a `SLOW:` line.
- `Curl.Console.UnitTests` fast run: 2661 passed, 24 skipped, 0 failed.

## Log

- 2026-10-07: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. Every test in the six CurlCommandRunnerR* test files writes ARRANGE, ACT and ASSERT lines through TestDiagnostics
