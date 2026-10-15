---
id: BL-2310
title: Remove Curl.Engine.UnitLibrary's reference to Curl.Cli.UnitLibrary so the engine reads only the easy option groups
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-2309, BL-2323, BL-2324, BL-2344, BL-2345, BL-2346, BL-2347, BL-2348, BL-2325, BL-2338, BL-2343, BL-2350, BL-2353, BL-2352, BL-2355, BL-2356, BL-2357, BL-2358]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.IntegrationTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2310 — Remove Curl.Engine.UnitLibrary's reference to Curl.Cli.UnitLibrary so the engine reads only the easy option groups

## Goal

`Curl.Engine.UnitLibrary` no longer references `Curl.Cli.UnitLibrary`, so the build itself proves the engine is free of the command line.

## Context

ADR-0506; plan phase 1 step 4 (closing step); follows BL-2309, after which no engine source names `CommandLineOptions`. Remove the `ProjectReference`; fix whatever else from `Curl.Cli` the engine still used (an enum or a parsed-value type) by passing a value in or defining the engine's own type; if the clean answer is moving a type to `Curl.Core` or a seam folder (outside this task's `touches`), move this task to `Blocked` and file that move as its dependency, per the task-board skill. `Curl.Engine.UnitTests` may keep referencing `Curl.Cli` only if a test needs to build a command line; prefer not.

## Acceptance criteria

- [x] `Curl.Engine.UnitLibrary/Curl.Engine.UnitLibrary.csproj` has no reference to `Curl.Cli.UnitLibrary`, and no engine source has `using Curl.Cli;`.
- [x] `Curl.Engine.UnitLibrary/CLAUDE.md` states that the engine must never reference `Curl.Cli`.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.
- [x] `dotnet publish Curl.Console -c Release` still produces a native binary, or the Notes say why it could not be run on this machine.

## Notes

- 2026-10-15 (lane 3): Measured by removing the `ProjectReference` and every `using Curl.Cli;` from the engine: 83 build errors remain, 92 uses of `CommandLineOptions` across 14 engine files (EngineComposition 37, TransferPerformer.Setup 13, HttpRequestOptionsMapping 9, TransferCredentialLookup 9, RedirectPolicyMapping 5, TransferProxySelection 4, and others), plus the types `CommandLineProxy`, `GssApiDelegation`, `IDataFileReader`, `ImapUploadFlags`, `InterfaceBinding`, `NetrcUse` and `RequestedHttpVersion`. BL-2309 left these. Backlog tasks BL-2325 (proxy), BL-2338 (authentication), BL-2343 (connect timeout/TransferContextFactory) and BL-2350 (HTTP version/ALPN) cover most of them, so they are now in `depends-on`.
- When this task is claimed again: repeat the removal (delete the csproj reference and the `using Curl.Cli;` lines), build `Curl.Engine.UnitLibrary`, and for any `CommandLineOptions` or type still named, retarget it here or file a task and requeue once. Then also fix `Curl.Engine.UnitTests` and `Curl.Console` call sites, update `Curl.Engine.UnitLibrary/CLAUDE.md` line 7 to say the engine must never reference `Curl.Cli`.
- 2026-10-15 (lane 2): Repeated the removal with every earlier dependency Done: 56 build errors remain, 51 of them `CommandLineOptions` (EngineComposition 32, TransferPerformer.Setup 12, MailRequestOptionsMapping, HttpRequestOptionsMapping, MultipartFormPartMapping, OptionGroupConnectionSettings, RedirectPolicyMapping, TransferContextFactory), plus `IDataFileReader` (SshKnownHostsFileSearch, TransferCredentialLookup), `ImapUploadFlags` and `InterfaceBinding`. Of the members read, `TraceComponents` alone has 56 uses; about 35 other members (Silent, ShowError, MaxTime, UseAlpn, Insecure, SslSessionsFile, Parallel, FailMode, CreateFileMode, AllowedProtocols and others) have no `Curl.Api` easy field yet. No task covered them, so filed BL-2355 (trace components), BL-2356 (EngineComposition factories, OptionGroupConnectionSettings, InterfaceBinding), BL-2357 (TransferPerformer.Setup, TransferContextFactory, RedirectPolicyMapping) and BL-2358 (HTTP/multipart/mail mappings, IDataFileReader), added them to `depends-on`, and requeued before any code edit; the removal was reverted.
- 2026-10-15 (lane 1): With every dependency Done, removing the `ProjectReference` and the three `using Curl.Cli;` lines (EngineComposition, EngineTransferSettings, HttpVersionMapping) left only two `<see cref="LocalPortRange..."/>` doc references in `EngineComposition.LocalBindingOf`, now pointing at `EasyNetworkOptions.LocalPort` and `LocalPortRange`. No engine code changed. `CLAUDE.md` now says the engine must never reference `Curl.Cli`.
- Choice: `Curl.Engine.UnitTests` and `Curl.Engine.IntegrationTests` now reference `Curl.Cli.UnitLibrary` directly (they had it only transitively). 33 engine test files build their option records from a parsed command line through the linked `EasyOptionsFromCommandLine.cs`, which the task allows; rewriting them to build the records by hand would weaken nothing but costs far more than this task. Added `Curl.Engine.IntegrationTests` to `touches` for that one csproj line; no task in Doing on `origin/work/dark-factory` named it.
- Measured: `dotnet build` clean; fast tests all green (Engine 812, Console 2785, Conformance 3129 passed, ratchet file unchanged); `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary`: 100% line, 100% branch, 0 failing members; `dotnet publish Curl.Console -c Release` produced a native `curl.exe`. `CurlEasyErrorText.cs` and `InProcessCurl` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Backlog. Engine still uses CommandLineOptions in 14 files; waits on BL-2325, BL-2338, BL-2343, BL-2350
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Backlog. Engine still uses CommandLineOptions in 10 files; waits on BL-2355, BL-2356, BL-2357, BL-2358
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Curl.Engine.UnitLibrary no longer references Curl.Cli; the build proves the engine reads only the option groups
