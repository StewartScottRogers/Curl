---
id: BL-2357
title: Make TransferPerformer.Setup, TransferContextFactory and RedirectPolicyMapping stop taking CommandLineOptions
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Api.UnitLibrary, Curl.Api.UnitTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2357 — Make TransferPerformer.Setup, TransferContextFactory and RedirectPolicyMapping stop taking CommandLineOptions

## Goal

No method in `Curl.Engine.UnitLibrary/TransferPerformer.Setup.cs`, `TransferContextFactory.cs` or `RedirectPolicyMapping.cs` takes `CommandLineOptions`.

## Context

ADR-0506; one of the four pieces BL-2310 needs before the engine can drop its `Curl.Cli` reference. Measured 2026-10-15 by BL-2310: TransferPerformer.Setup has 12 `CommandLineOptions` parameters (including RefuseMalformedSetopt, ReportKnownHostsFileMissingAsync, RefuseMalformedInterface, RefuseMalformedEchMode), TransferContextFactory one, RedirectPolicyMapping.Of one. Members read include Silent, ShowError, MaxTime, FailMode, CreateFileMode, EchModeIsMalformed, DisallowUsernameInUrl, AllowedProtocols, AllowedRedirectProtocols, SslRequired, SslLevel, FtpSslControlOnly, ListOnly and Interface. Read each from the `Curl.Api` easy groups, adding a field where libcurl has the matching `CURLOPT_*`; for curl-tool-only settings (silent, show-error, fail mode, create-file-mode, malformed-value refusals) define an engine-owned settings type in `Curl.Engine.UnitLibrary` that `Curl.Console` fills.

## Acceptance criteria

- [x] `TransferPerformer.Setup.cs`, `TransferContextFactory.cs` and `RedirectPolicyMapping.cs` do not name `CommandLineOptions`.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes with no test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1` passes all four gates for every library changed, measured in one call.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged.

## Notes

- Decided (ADR-0539): tool-only settings are an engine-owned `EngineTransferSettings`; `--path-as-is`, `--interface`, `--connect-to` come from `EasyNetworkOptions`. `HttpRequestOptionsMapping.Of` and `MailRequestOptionsMapping.FromCommandLine` also changed to take the record, as `TransferContextFactory.Create` calls them.
- Quality gates passed for Curl.Engine.UnitLibrary and Curl.Console in one run; PassingUpstreamCases.txt untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. Setup, context factory and redirect mapping read EngineTransferSettings; build clean, fast tests green, quality gates pass
