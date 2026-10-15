---
id: BL-2353
title: Thread EasyAuthenticationOptions through EngineComposition's transport factories for Negotiate and proxy authentication
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2338]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Engine.IntegrationTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2353 — Thread EasyAuthenticationOptions through EngineComposition's transport factories for Negotiate and proxy authentication

## Goal

`EngineComposition`'s `CreateTransports` (both overloads), `CreateTcpConnector`, `CreatePoolingConnector` and `CreateProxyTunnelOptions` take `EasyAuthenticationOptions`, and `NegotiateOptionsMapping` maps that record (`Of`, replacing `FromCommandLine`), so the SOCKS5 GSS-API and proxy tunnel's Negotiate service names, `--delegation` and the proxy's auth methods no longer come from `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2338 (ADR-0506 plan phase 1 step 4; mapping settled in ADR-0516). Today `CreateTcpConnector` calls `NegotiateOptionsMapping.FromCommandLine(options)` for `Socks5AuthenticationMapping.Of`, and `CreateProxyTunnelOptions` reads `options.ProxyAuthSchemes` and `NegotiateOptionsMapping.FromCommandLine(options)`. `Curl.Console/CurlComposition.cs` calls `FromCommandLine` three times (lines ~86, ~331, ~487). About 100 test call sites pass `EasyOptionsFromCommandLine.ProxyOf(x)` to these factories; add `EasyOptionsFromCommandLine.AuthenticationOf(x)` beside each, as BL-2325 added `ProxyOf`. `ProxyAuthentication` is `CURLAUTH_*` bits (`HttpAuthenticationMethods`): the engine needs a mapping back to `HttpAuthSchemes` (`Any` to `Any`, each bit to its scheme). `AwsSigV4HttpAuthenticator` reads no option at all (BL-2338 Notes). Follow the retarget rules in BL-2303's Context.

## Acceptance criteria

- [ ] `NegotiateOptionsMapping.Of(EasyAuthenticationOptions)` replaces `FromCommandLine`; `CreateTcpConnector`, `CreateProxyTunnelOptions`, `CreatePoolingConnector` and `CreateTransports` take `EasyAuthenticationOptions` and read no authentication setting from `CommandLineOptions`.
- [ ] Tests that built `CommandLineOptions` only to drive one of those members now build the record; tests through the whole command line stay as they are.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
