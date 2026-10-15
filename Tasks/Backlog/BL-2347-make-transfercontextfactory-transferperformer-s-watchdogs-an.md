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
completed:
---
# BL-2347 — Make TransferContextFactory, TransferPerformer's watchdogs and HttpRequestOptionsMapping's fail and method read EasyBehaviourOptions and EasyTransferOptions

## Goal

The engine reads its behaviour and transfer settings - verbose, fail-on-error, range, resume, upload, file time, time condition, text mode, CRLF, speed limits, low-speed and maximum time, custom request, no-body, max file size - from the `EasyBehaviourOptions` and `EasyTransferOptions` that `EasyOptionsFromCommandLine.BehaviourOf`, `TransferOf` and `UploadTransferOf` build (BL-2308), instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4. Split out of BL-2308 before any code, as BL-2323 and BL-2344 to BL-2346 were split out of BL-2303 and BL-2307. The rules for every retarget task are in BL-2303's Context; BL-2308's Notes and ADR-0528 record the mapping.

Engine readers as of 2026-10-15: `TransferContextFactory` (`Range`, `ResumeFrom`/`ResumeFromOutputSize`, `MaxFileSize`, `UseAscii`, `ConvertLineEndings`, `MaxTime`, `TimeCondition`, `RemoteTime`, `NoBody`, the upload size), `TransferPerformer.StartMaxTimeWatchdog` and the `-Y`/`-y` `LowSpeedWatchdog` start, `EngineComposition`'s `TransferTimers` (`MaxTime`), `HttpRequestOptionsMapping` (`RequestMethod`, `FailMode`) and `MailRequestOptionsMapping` (`RequestMethod`).

The records keep no "given as zero" for `-m` and no `-C -`: where the engine needs them (`-C -` resuming from the output file's size) it takes them per transfer from the tool, as `UploadTransferOf` takes the upload size. `--fail-with-body` is not `CURLOPT_FAILONERROR` (curl's tool checks the response code itself), so it stays a tool input beside the record. Tests that built `CommandLineOptions` only to drive these classes now build the records; tests through the whole command line stay as they are.

## Acceptance criteria

- [ ] `TransferContextFactory`, the `-m` and `-Y`/`-y` watchdog starts, `TransferTimers`' transfer timeout, `HttpRequestOptionsMapping`'s method and fail mode and `MailRequestOptionsMapping`'s command take `EasyBehaviourOptions`/`EasyTransferOptions`, not `CommandLineOptions`, for these settings.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Engine.UnitLibrary` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [ ] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
