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
completed: 2026-10-15
---
# BL-2308 — Retarget the engine's behaviour and transfer settings from CommandLineOptions to EasyBehaviourOptions and EasyTransferOptions

## Goal

The engine reads its behaviour and transfer settings - verbose, fail-on-error, range, resume, upload, file time, time condition, text mode, CRLF, speed limits, low-speed and maximum time - from `EasyBehaviourOptions` and `EasyTransferOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds them; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4; follows BL-2307 and uses the rules for every retarget task written in BL-2303's Context. Engine readers (as of 2026-10-15): `TransferContextFactory` (range, resume, upload size, text mode, CRLF), `TransferPerformer`'s watchdogs (`LowSpeedWatchdog`, `MaxTimeWatchdog` from `Curl.Core`), the rate limiting, `-f`/`--fail-with-body` handling, `-z` time conditions, `-R` file time request. Tool-side, not retargeted: the progress meter, `-#`, `-s`/`-S` message printing, `--retry*`, output files. `-v` and `--trace*` map to `CURLOPT_VERBOSE` and a debug callback set by the tool; the engine's trace events keep working through `ITransferEvents` (the callback wiring proper is phase 3).

## Acceptance criteria

- [x] ~~The engine readers named in Context take `EasyBehaviourOptions`/`EasyTransferOptions`, not `CommandLineOptions`, for these settings.~~ — split out to BL-2347 (see Notes).
- [x] `EasyOptionsFromCommandLine` maps every command-line option of the two groups (`-r`, `-C`, `-T` size, `-R`, `-z`, `-B`, `--crlf`, `-f`, `--fail-with-body`, `--limit-rate`, `-Y`, `-y`, `-m`, `--max-filesize`, `-l`, `-I`, `-X` as `CUSTOMREQUEST`, `-v`, `-i`, `--no-buffer` where it reaches libcurl, and the rest), each with a test.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- 2026-10-15, split (rule 4, before any code), as BL-2303 and BL-2307 split: the engine retarget (`TransferContextFactory`, the `-m` and `-Y`/`-y` watchdog starts, `TransferTimers`, `HttpRequestOptionsMapping`'s method and fail mode, `MailRequestOptionsMapping`'s command, and their test call sites) does not fit one run beside the mapper. This task delivers `EasyOptionsFromCommandLine.BehaviourOf`, `TransferOf`, `UploadTransferOf` and ADR-0528; filed BL-2347 (depends on this) for the retarget, and BL-2310 now also depends on BL-2347. Neither record needed a new member, so `Curl.Api.UnitLibrary/Options/Behaviour` is unchanged.
- Decided (ADR-0528, by Claude under Stewart's delegation): `NoProgress` is off unless `-s`/`--no-progress-meter` (curl's tool sets 0 for its callback); `FailOnError` is `-f` only, `--fail-with-body` being the tool's check; `BufferSize` is the tool's 100 KiB or a smaller `--limit-rate`; `-C -` and `-T` are per transfer (`UploadTransferOf(transfer, size)` adds the upload, size `-1` when unknown). Where the command line wins over `--libcurl`: `IncludeHeader` is on for `-i` and `-I` (curl 8 writes no `CURLOPT_HEADER`, its header callback does the work). `-S`, `-#`, `-N` and `--retry*` never reach libcurl.
- `--ai-help` unaffected (no option changes).
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console` - first run had `WithSpeeds` at complexity 16, split into `WithLowSpeed` and `WithRateAndTimeout`; second run 100% line and branch, 0 failing members, worst CRAP 10. Fast tests: every assembly green (Console 2734 passed, Engine 652, Conformance 3129). Ratchet file unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyOptionsFromCommandLine.BehaviourOf, TransferOf and UploadTransferOf map the behaviour and transfer groups (ADR-0528); engine retarget split to BL-2347
