---
id: BL-2347
title: Make TransferContextFactory, TransferPerformer's watchdogs and HttpRequestOptionsMapping's fail and method read EasyBehaviourOptions and EasyTransferOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2308]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Engine.IntegrationTests, Curl.Api.UnitLibrary/Options/Behaviour, Curl.Api.UnitTests/Options/Behaviour]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2347 — Make TransferContextFactory, TransferPerformer's watchdogs and HttpRequestOptionsMapping's fail and method read EasyBehaviourOptions and EasyTransferOptions

## Goal

The engine reads its behaviour and transfer settings - verbose, fail-on-error, range, resume, upload, file time, time condition, text mode, CRLF, speed limits, low-speed and maximum time, custom request, no-body, max file size - from the `EasyBehaviourOptions` and `EasyTransferOptions` that `EasyOptionsFromCommandLine.BehaviourOf`, `TransferOf` and `UploadTransferOf` build (BL-2308), instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4. Split out of BL-2308 before any code, as BL-2323 and BL-2344 to BL-2346 were split out of BL-2303 and BL-2307. The rules for every retarget task are in BL-2303's Context; BL-2308's Notes and ADR-0528 record the mapping.

Engine readers as of 2026-10-15: `TransferContextFactory` (`Range`, `ResumeFrom`/`ResumeFromOutputSize`, `MaxFileSize`, `UseAscii`, `ConvertLineEndings`, `MaxTime`, `TimeCondition`, `RemoteTime`, `NoBody`, the upload size), `TransferPerformer.StartMaxTimeWatchdog` and the `-Y`/`-y` `LowSpeedWatchdog` start, `EngineComposition`'s `TransferTimers` (`MaxTime`), `HttpRequestOptionsMapping` (`RequestMethod`, `FailMode`) and `MailRequestOptionsMapping` (`RequestMethod`).

The records keep no "given as zero" for `-m` and no `-C -`: where the engine needs them (`-C -` resuming from the output file's size) it takes them per transfer from the tool, as `UploadTransferOf` takes the upload size. `--fail-with-body` is not `CURLOPT_FAILONERROR` (curl's tool checks the response code itself), so it stays a tool input beside the record. Tests that built `CommandLineOptions` only to drive these classes now build the records; tests through the whole command line stay as they are.

## Acceptance criteria

- [x] `TransferContextFactory`, the `-m` and `-Y`/`-y` watchdog starts, `TransferTimers`' transfer timeout, `HttpRequestOptionsMapping`'s method and fail mode and `MailRequestOptionsMapping`'s command take `EasyBehaviourOptions`/`EasyTransferOptions`, not `CommandLineOptions`, for these settings.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Engine.UnitLibrary` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- Plan (one pass, before code): every reader is in `Curl.Engine.UnitLibrary`, called from `Curl.Console`'s `CurlCommandRunner`; tests in the touched test projects. BL-2308 is Done; no prerequisite missing.
- Decided (ADR-0534, by Claude under Stewart's delegation): `EasyTransferOptions.TimeValue` is now whole seconds (`long`, libcurl's `CURLOPT_TIMEVALUE_LARGE`), because a `DateTimeOffset` clamps a `-z` date past 9999 that ADR-0410 keeps; `-C -` with `-T` is `Upload` with `ResumeFrom` -1, set per transfer by the runner's new `TransferOptionsOf` (upload size left unknown, -1); `--max-filesize 0` and `-m 0` reach the context as `null` (libcurl's 0 is none; only SMTP's command output read a 0 limit as a limit, failing with exit 63, and now matches curl); `RemoteTime` is `FileTime` (FTP already asked `MDTM` under `-I`, and the runner applies the time only under `-R`); `Fail` is `Fail` under `FailOnError`, else `CommandLineOptions.FailMode` (`--fail-with-body` stays the tool's).
- Test call sites were rewritten mechanically with `sed` (31 `Create` calls of the same shape); the `-C -` test now drives the record directly, and new tests pin a `-z` date past 9999 and zero `--max-filesize`/`-m`.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console,Curl.Api.UnitLibrary`: Api and Console 100/100, 0 failing; Engine 2 failing - `TransferContextFactory.Create` at complexity 12 (the two zero-to-null ternaries moved into `MaxFileSizeOf` and `MaxTimeOf`, now 10) and `TransferTimers.Positive`'s unreachable null branch (now takes a `TimeSpan`). Not re-run (rule 9); the Engine and Console tests re-ran green after the fix.
- Fast tests: every assembly green (Engine 677, Console 2826, Conformance 3129 passed). `PassingUpstreamCases.txt` unchanged; `CurlEasyErrorText.cs` and `InProcessCurl.RunAsync` untouched. `--ai-help` unaffected (no option changes).

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. TransferContextFactory, the -m and -Y/-y watchdog starts, TransferTimers, HttpRequestOptionsMapping's -X and -f and MailRequestOptionsMapping's -X read EasyBehaviourOptions and EasyTransferOptions (ADR-0534)
