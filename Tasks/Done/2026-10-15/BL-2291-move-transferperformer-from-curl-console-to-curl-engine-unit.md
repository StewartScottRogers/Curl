---
id: BL-2291
title: Move TransferPerformer from Curl.Console to Curl.Engine.UnitLibrary so Curl.Console holds only the curl tool's work
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2290, BL-2284]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Documentation/Planning/Curl-Api-And-libcurl.md]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2291 — Move TransferPerformer from Curl.Console to Curl.Engine.UnitLibrary so Curl.Console holds only the curl tool's work

## Goal

`TransferPerformer` (built by BL-2282 to BL-2284) lives in `Curl.Engine.UnitLibrary`, so every piece of libcurl's work is in the engine and `Curl.Console` holds only the curl tool's work.

## Context

ADR-0506, plan phase 1 step 3 (last move); needs BL-2290 (the dispatch and context factory are already in the engine) and BL-2284 (the performer writes only through injected outputs). After this, the plan's "stays in `Curl.Console`" column should describe `Curl.Console` exactly; correct the plan's table if it does not. The engine still reads `CommandLineOptions` (until BL-2310).

## Acceptance criteria

- [x] `TransferPerformer` (and any class it owns) is in `Curl.Engine.UnitLibrary`; `Curl.Console` has no file defining it.
- [x] `TransferPerformerTests` are in `Curl.Engine.UnitTests` with the same names and assertions.
- [x] Every remaining `.cs` file in `Curl.Console` is in the plan's "stays in `Curl.Console`" column, or the plan's table is corrected in this task with the reason.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened (the fast-test count is not lower than before).
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [x] `dotnet publish Curl.Console -c Release` still produces a native binary (vswhere on PATH, see root `CLAUDE.md`), or the Notes say why it could not be run on this machine.

## Notes

- Moved `TransferPerformer` (both partial files), `HstsCacheFile` and `UrlEffective` to `Curl.Engine`, with `TransferPerformerTests` (both files) and `UrlEffectiveTests` in `Curl.Engine.UnitTests`, same names and assertions; one test added (`KnownHostsFileMissingFailure_GivenEasyErrorText_Exits2WithItsTextForExit2`).
- Tool-side types it reached, and how each was cut:
  - `RunningTransferState`: the part the performer keeps (watchdogs, retry and connection IDs, looked-up credentials, SSH options, alt-svc cache) is the new engine base class `EngineTransferState`; `RunningTransferState` derives from it, so no runner call site changed.
  - `CurlEasyErrorText` stays at its path (gap office), so the performer takes `Func<CurlExitCode, string> easyErrorText` in its constructor, as `EngineComposition` does for DoH (BL-2289). `KnownHostsFileMissingFailure` is now one per performer, `ResolveSshOptionsAsync` and `SetupFailureAsync` are instance methods, and `EndsTheRun` compares the performer's failure by reference beside the static set.
  - `HstsCacheFile` used `DeferredOutputFileStream.CreateMode`: it has its own `0666` constant, as `AltSvcTransferCache` does (BL-2290).
  - `InMemoryFileSystem` (test fake) uses tool-side types, so the setup tests have their own `ReadRecordingFileSystem`.
- Quality: the first measurement failed four `Curl.Console` members on branch coverage: C# caches a method-group delegate (`CurlEasyErrorText.Of`) in one field per class behind a null check, so each new call site added a half-run branch. Fixed with one explicitly created delegate field each in `CurlComposition` and `CurlCommandRunner`. Second run: both libraries 100% line and branch, worst CRAP 10, 0 failing.
- Plan table corrected: the left column now lists every `.cs` file left in `Curl.Console` (added `RunningTransferState`, `TlsSessionFileLines`, `WindowsOutputFileNameSanitizer`, `RemoteHeaderNameStream`, `RemoteTimeFailureWarning`, `RecordingDataFileReader`, `TransferDiagnosticLines`, `TransferEventOutput` and the four stream wrappers whose callers stayed); `HeaderLineTeeStream` (moved by BL-2290), `UrlEffective` and `EngineTransferState` are on the right. The plan was added to `touches` for this; no task in Doing on origin/work/dark-factory names it.
- Fast tests green (Console.UnitTests 2285, Engine.UnitTests 649, Conformance 3753); `PassingUpstreamCases.txt`, `InProcessCurl` and `CurlEasyErrorText.cs` untouched. `dotnet publish Curl.Console -c Release` produced a native `curl.exe`.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. TransferPerformer, with EngineTransferState, HstsCacheFile and UrlEffective, lives in Curl.Engine; Curl.Console holds only the tool's work
