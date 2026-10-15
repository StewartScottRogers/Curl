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
completed:
---
# BL-2289 — Move CurlComposition's handler, transport, resolver, TLS and security-context factories to an EngineComposition in Curl.Engine.UnitLibrary

## Goal

The factories that build protocol handlers, transports, DNS resolvers, TLS providers, security-context factories and authenticators live in an `EngineComposition` class in `Curl.Engine.UnitLibrary`; `Curl.Console`'s `CurlComposition` keeps only `CreateRunner` and the runner wiring, and calls `EngineComposition`.

## Context

ADR-0506, plan phase 1 step 3; follows BL-2288. `Curl.Console/CurlComposition.cs` (~110 KB) is the composition root, written as plain constructor calls (no container, no reflection) - keep it that way, native AOT depends on it. Move (as of 2026-10-15): `CreateProtocolHandlers`, `LdapDialectFor`, `SshAlgorithmPreferencesFor`, `CreateFtpProtocolHandler`, `FtpDataConnectorOf`, `CreateHttpAuthenticator`, `CreateSecurityContextFactory`, `CreateDialingSecurityContextFactory`, `CreateSaslAuthenticator`, both `CreateTransports`, `CreateDnsResolver`, `CreateDohResolver`, the `Traces*` predicates, `DohUrlOf`, `CreateDohConnector`, `CreateTlsProvider`, `WaitsForMultiplexing`, `CreatePoolingConnector`, `CreateTcpConnector`, `PreProxyOf`, `HaproxyProtocolOf`, `LocalBindingOf`, `UnixSocketOf`, `AddressFamilyOf`, `ConnectTimeoutOf`, `CreateUdpDatagramConnector`, `CreateProxyTunnelOptions`, `UsesAltSvcOverHttp`, `LearnsHstsOverHttp`, `HstsClockOf`, `DnsServerVariableResolverOf`, `CreateDatagramConnectorQuicDialer`, `NumberedDatagramsOf`, `ConnectionNumbersOf`. `WriteOutTimeDialectFor` and `CreateRunner` stay.

**`CurlEasyErrorText` stays in `Curl.Console`**: `Gap/Tools/Measure-ExitCodeGap.ps1` reads `Curl.Console/CurlEasyErrorText.cs` by path, and `Gap/` changes only from an interactive session (ADR-0506). `CreateDohResolver` uses `CurlEasyErrorText.Of`; give `EngineComposition` that text as a `Func<CurlExitCode, string>` parameter, which `CurlComposition` supplies.

Tests of these factories (`CurlCompositionTests` and the like in `Curl.Console.UnitTests`, and `Curl.Console.IntegrationTests/CurlCompositionIntegrationTests.cs`, `CurlCompositionDnsServersIntegrationTests.cs`) move with what they test; Integration tests go to a new `Curl.Engine.IntegrationTests` project only if one is needed (it sorts directly before `Curl.Engine.UnitLibrary`; every test in it carries `[TestCategory("Integration")]`, ADR-0421) - in that case add it to `Curl.slnx` and note it here.

## Acceptance criteria

- [ ] `Curl.Engine.UnitLibrary/EngineComposition.cs` holds the factories named in Context; `Curl.Console/CurlComposition.cs` declares none of them and calls `EngineComposition`.
- [ ] `Curl.Console/CurlEasyErrorText.cs` is still at that path, and `EngineComposition` receives the DoH error text through a parameter.
- [ ] The moved factories' tests are in `Curl.Engine.UnitTests` (or `Curl.Engine.IntegrationTests` for Integration tests) with the same names and assertions.
- [ ] `dotnet build` is clean (warnings are errors), including `VerifyIntegrationTestPlacement`.
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened; `dotnet test --filter "TestCategory=Integration"` passes as before the task.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes.
- [ ] `InProcessCurl.RunAsync`'s public signatures are unchanged (`Gap/Tools/Measure-UpstreamCases.cs` calls them).
- [ ] `Curl.Console/CLAUDE.md` and `Curl.Engine.UnitLibrary/CLAUDE.md` each describe only the code in their own project.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
