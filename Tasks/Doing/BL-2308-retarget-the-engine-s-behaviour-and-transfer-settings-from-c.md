---
id: BL-2308
title: Retarget the engine's behaviour and transfer settings from CommandLineOptions to EasyBehaviourOptions and EasyTransferOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2307, BL-2294]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Options/Behaviour, Curl.Api.UnitTests/Options/Behaviour]
requirement: none
created: 2026-10-15
completed:
---
# BL-2308 — Retarget the engine's behaviour and transfer settings from CommandLineOptions to EasyBehaviourOptions and EasyTransferOptions

## Goal

The engine reads its behaviour and transfer settings - verbose, fail-on-error, range, resume, upload, file time, time condition, text mode, CRLF, speed limits, low-speed and maximum time - from `EasyBehaviourOptions` and `EasyTransferOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds them; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4; follows BL-2307 and uses the rules for every retarget task written in BL-2303's Context. Engine readers (as of 2026-10-15): `TransferContextFactory` (range, resume, upload size, text mode, CRLF), `TransferPerformer`'s watchdogs (`LowSpeedWatchdog`, `MaxTimeWatchdog` from `Curl.Core`), the rate limiting, `-f`/`--fail-with-body` handling, `-z` time conditions, `-R` file time request. Tool-side, not retargeted: the progress meter, `-#`, `-s`/`-S` message printing, `--retry*`, output files. `-v` and `--trace*` map to `CURLOPT_VERBOSE` and a debug callback set by the tool; the engine's trace events keep working through `ITransferEvents` (the callback wiring proper is phase 3).

## Acceptance criteria

- [ ] The engine readers named in Context take `EasyBehaviourOptions`/`EasyTransferOptions`, not `CommandLineOptions`, for these settings.
- [ ] `EasyOptionsFromCommandLine` maps every command-line option of the two groups (`-r`, `-C`, `-T` size, `-R`, `-z`, `-B`, `--crlf`, `-f`, `--fail-with-body`, `--limit-rate`, `-Y`, `-y`, `-m`, `--max-filesize`, `-l`, `-I`, `-X` as `CUSTOMREQUEST`, `-v`, `-i`, `--no-buffer` where it reaches libcurl, and the rest), each with a test.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
