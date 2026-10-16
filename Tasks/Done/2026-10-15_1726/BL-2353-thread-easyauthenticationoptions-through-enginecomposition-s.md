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
completed: 2026-10-15
---
# BL-2353 — Thread EasyAuthenticationOptions through EngineComposition's transport factories for Negotiate and proxy authentication

## Goal

`EngineComposition`'s `CreateTransports` (both overloads), `CreateTcpConnector`, `CreatePoolingConnector` and `CreateProxyTunnelOptions` take `EasyAuthenticationOptions`, and `NegotiateOptionsMapping` maps that record (`Of`, replacing `FromCommandLine`), so the SOCKS5 GSS-API and proxy tunnel's Negotiate service names, `--delegation` and the proxy's auth methods no longer come from `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2338 (ADR-0506 plan phase 1 step 4; mapping settled in ADR-0516). Today `CreateTcpConnector` calls `NegotiateOptionsMapping.FromCommandLine(options)` for `Socks5AuthenticationMapping.Of`, and `CreateProxyTunnelOptions` reads `options.ProxyAuthSchemes` and `NegotiateOptionsMapping.FromCommandLine(options)`. `Curl.Console/CurlComposition.cs` calls `FromCommandLine` three times (lines ~86, ~331, ~487). About 100 test call sites pass `EasyOptionsFromCommandLine.ProxyOf(x)` to these factories; add `EasyOptionsFromCommandLine.AuthenticationOf(x)` beside each, as BL-2325 added `ProxyOf`. `ProxyAuthentication` is `CURLAUTH_*` bits (`HttpAuthenticationMethods`): the engine needs a mapping back to `HttpAuthSchemes` (`Any` to `Any`, each bit to its scheme). `AwsSigV4HttpAuthenticator` reads no option at all (BL-2338 Notes). Follow the retarget rules in BL-2303's Context.

## Acceptance criteria

- [x] `NegotiateOptionsMapping.Of(EasyAuthenticationOptions)` replaces `FromCommandLine`; `CreateTcpConnector`, `CreateProxyTunnelOptions`, `CreatePoolingConnector` and `CreateTransports` take `EasyAuthenticationOptions` and read no authentication setting from `CommandLineOptions`.
- [x] Tests that built `CommandLineOptions` only to drive one of those members now build the record; tests through the whole command line stay as they are.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

- Delivered directly rather than through the /feature stages: a mechanical signature retarget with no design open beyond ADR-0516, so a protocol-architect plan would have cost more than it saved.
- `CreatePoolingConnector` reads no authentication setting (only `-Z`, ALPN, `--ssl-sessions` and the network, TLS, HTTP and proxy groups), so it takes no record: an unused parameter would be a name that lies. Every other member named in the Goal now takes `EasyAuthenticationOptions`.
- `HttpRequestOptionsMapping.ProxyAuthSchemesOf` maps `ProxyAuthentication` back to `HttpAuthSchemes`: `Any` to `Any` (never Bearer), each bit to its scheme, Basic when none is left; the CLI always sets one scheme or `Any`, so the round trip is exact.
- `CurlComposition`'s three `FromCommandLine` calls now map `EasyOptionsFromCommandLine.AuthenticationOf(options)`; the CLI's `options.ProxyAuthSchemes` passed to `CreateTransferDispatch` stays tool-side. ~100 test call sites gained `AuthenticationOf(x)` beside `ProxyOf(x)` (paren-balanced scripted insert). New record-driven tests: `Curl.Engine.UnitTests/EasyAuthenticationOptionsTransportTests.cs`.
- Measured: build clean; fast tests green (Console 2818, Engine 751, Conformance 3129 passed); `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. NegotiateOptionsMapping.Of and EngineComposition's transport, TCP connector and proxy tunnel factories read Negotiate names, --delegation and proxy auth methods from EasyAuthenticationOptions
