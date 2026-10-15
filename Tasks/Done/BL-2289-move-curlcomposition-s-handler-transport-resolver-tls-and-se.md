---
id: BL-2289
title: Move CurlComposition's handler, transport, resolver, TLS and security-context factories to an EngineComposition in Curl.Engine.UnitLibrary
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-2288]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Console.IntegrationTests, Curl.Engine.IntegrationTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.slnx]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2289 — Move CurlComposition's handler, transport, resolver, TLS and security-context factories to an EngineComposition in Curl.Engine.UnitLibrary

## Goal

The factories that build protocol handlers, transports, DNS resolvers, TLS providers, security-context factories and authenticators live in an `EngineComposition` class in `Curl.Engine.UnitLibrary`; `Curl.Console`'s `CurlComposition` keeps only `CreateRunner` and the runner wiring, and calls `EngineComposition`.

## Context

ADR-0506, plan phase 1 step 3; follows BL-2288. `Curl.Console/CurlComposition.cs` (~110 KB) is the composition root, written as plain constructor calls (no container, no reflection) - keep it that way, native AOT depends on it. Move (as of 2026-10-15): `CreateProtocolHandlers`, `LdapDialectFor`, `SshAlgorithmPreferencesFor`, `CreateFtpProtocolHandler`, `FtpDataConnectorOf`, `CreateHttpAuthenticator`, `CreateSecurityContextFactory`, `CreateDialingSecurityContextFactory`, `CreateSaslAuthenticator`, both `CreateTransports`, `CreateDnsResolver`, `CreateDohResolver`, the `Traces*` predicates, `DohUrlOf`, `CreateDohConnector`, `CreateTlsProvider`, `WaitsForMultiplexing`, `CreatePoolingConnector`, `CreateTcpConnector`, `PreProxyOf`, `HaproxyProtocolOf`, `LocalBindingOf`, `UnixSocketOf`, `AddressFamilyOf`, `ConnectTimeoutOf`, `CreateUdpDatagramConnector`, `CreateProxyTunnelOptions`, `UsesAltSvcOverHttp`, `LearnsHstsOverHttp`, `HstsClockOf`, `DnsServerVariableResolverOf`, `CreateDatagramConnectorQuicDialer`, `NumberedDatagramsOf`, `ConnectionNumbersOf`. `WriteOutTimeDialectFor` and `CreateRunner` stay.

**`CurlEasyErrorText` stays in `Curl.Console`**: `Gap/Tools/Measure-ExitCodeGap.ps1` reads `Curl.Console/CurlEasyErrorText.cs` by path, and `Gap/` changes only from an interactive session (ADR-0506). `CreateDohResolver` uses `CurlEasyErrorText.Of`; give `EngineComposition` that text as a `Func<CurlExitCode, string>` parameter, which `CurlComposition` supplies.

Tests of these factories (`CurlCompositionTests` and the like in `Curl.Console.UnitTests`, and `Curl.Console.IntegrationTests/CurlCompositionIntegrationTests.cs`, `CurlCompositionDnsServersIntegrationTests.cs`) move with what they test; Integration tests go to a new `Curl.Engine.IntegrationTests` project only if one is needed (it sorts directly before `Curl.Engine.UnitLibrary`; every test in it carries `[TestCategory("Integration")]`, ADR-0421) - in that case add it to `Curl.slnx` and note it here.

## Acceptance criteria

- [x] `Curl.Engine.UnitLibrary/EngineComposition.cs` holds the factories named in Context; `Curl.Console/CurlComposition.cs` declares none of them and calls `EngineComposition`.
- [x] `Curl.Console/CurlEasyErrorText.cs` is still at that path, and `EngineComposition` receives the DoH error text through a parameter.
- [x] The moved factories' tests are in `Curl.Engine.UnitTests` (or `Curl.Engine.IntegrationTests` for Integration tests) with the same names and assertions.
- [x] `dotnet build` is clean (warnings are errors), including `VerifyIntegrationTestPlacement`.
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened; `dotnet test --filter "TestCategory=Integration"` passes as before the task.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [x] `InProcessCurl.RunAsync`'s public signatures are unchanged (`Gap/Tools/Measure-UpstreamCases.cs` calls them).
- [x] `Curl.Console/CLAUDE.md` and `Curl.Engine.UnitLibrary/CLAUDE.md` each describe only the code in their own project.

## Notes

- Moved by line range, unchanged, into `Curl.Engine.UnitLibrary/EngineComposition.cs` (`internal static class`, namespace `Curl.Engine`), with the private helpers and constants they use. `CurlComposition.cs` (111 KB to about 30 KB) keeps `CreateRunner`, the dispatch builders, `WriteOutTimeDialectFor`, `SharingRunCookies`, `WarningLinesBeforeEachTransfer` and `GroupConnectorOf`, and calls the engine through `using static Curl.Engine.EngineComposition;`, so the plain constructor calls are unchanged.
- `OptionGroupConnectionSettings` and `ProcessUserId` (with `ProcessUserIdTests`) moved too: only the moved factories use them, and the engine cannot reference `Curl.Console`.
- DoH error text: `CreateTransports`, `CreateDnsResolver` and `CreateDohResolver` take an optional `Func<CurlExitCode, string>? easyErrorText`, which `CurlComposition` sets to `CurlEasyErrorText.Of`; without one the text is empty. Optional rather than required (sensible default) so the ~100 test calls of `CreateTransports` stay as they were; production always passes it.
- Tests: 23 files that test only the moved factories moved to `Curl.Engine.UnitTests` as `EngineComposition*Tests` (same test names and assertions), with copies of the `LoopbackDnsResolver` and `ScriptedTcpDialer` fakes and a reference to `Curl.Protocol.Ssh.UnitTests`. Files that run a transfer through `CreateRunner` or `CreateTransferDispatch` stay in `Curl.Console.UnitTests`. A new `Curl.Engine.IntegrationTests` project (in `Curl.slnx`, before `Curl.Engine.UnitLibrary`) holds `EngineCompositionDnsServersIntegrationTests`.
- Left for BL-2322: the factory tests inside the mixed `CurlCompositionTests` and `CurlCompositionProxyTests` partial classes, and moving the `Curl.Console/CLAUDE.md` paragraphs that describe `EngineComposition` members' behaviour (that file now says so and points at the engine's CLAUDE.md).
- Measured: build clean; fast tests green (Engine.UnitTests 517, Console.UnitTests 2390, Conformance 3112 passed); Integration: Engine 1, Console 2 passed; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console`: 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. CurlComposition's factories live in Curl.Engine's EngineComposition, which CurlComposition calls; their tests moved with them (rest in BL-2322)
