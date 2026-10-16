---
id: BL-2325
title: Make the engine's proxy classes read EasyProxyOptions instead of CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2305, BL-2323]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Engine.IntegrationTests, Curl.Api.UnitLibrary/Options/Proxy, Curl.Api.UnitTests/Options/Proxy]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2325 — Make the engine's proxy classes read EasyProxyOptions instead of CommandLineOptions

## Goal

The engine's proxy classes read `EasyProxyOptions` instead of `CommandLineOptions`, and `Curl.Console` hands them the record `EasyOptionsFromCommandLine.ProxyOf` builds; behaviour is byte for byte unchanged.

## Context

ADR-0506 plan phase 1 step 4, split out of BL-2305 (which built `EasyOptionsFromCommandLine.ProxyOf` and settled the mapping in ADR-0515). Engine classes to retarget: `TransferProxySelection`, `EngineComposition.PreProxyOf`, `HaproxyProtocolOf`, `CreateProxyTunnelOptions`, `Socks5AuthenticationMapping`, the proxy selection in `TransferPerformer`, and `TransferContextFactory.ConnectToTunnelsThroughProxy`. Follow the retarget rules in BL-2303's Context. Depends on BL-2323, which gives `Curl.Engine.UnitLibrary` its reference to `Curl.Api.UnitLibrary`.

Points ADR-0515 settles that this retarget must honour:
- The proxy environment variables stay the engine's to read (libcurl reads them); the record carries only what `CURLOPT_PROXY`/`CURLOPT_NOPROXY` set, `null` meaning "the environment decides".
- `ProxyType` is the option's kind (`--proxy1.0` is `Http10`, `--socks*` the SOCKS kinds) and a scheme in `Proxy` outranks it; `--proxy-http2` with `-x` is `Https2`, which the engine reads where it reads `ProxyHttp2` today.
- `Socks5Authentication` with neither `--socks5-basic` nor `--socks5-gssapi` is libcurl's `Basic | GssApi`; check that `Socks5AuthenticationMapping` reads that default as it reads "neither given" today.
- Proxy credentials and `--proxy-service-name` are `EasyAuthenticationOptions`', proxy TLS `EasyTlsOptions.Proxy`: keep passing them beside the record until their own retargets (BL-2324 and the authentication group's).

## Acceptance criteria

- [x] `TransferProxySelection`, `EngineComposition.PreProxyOf`, `HaproxyProtocolOf`, `CreateProxyTunnelOptions`, `Socks5AuthenticationMapping`, `TransferPerformer`'s proxy selection and `TransferContextFactory.ConnectToTunnelsThroughProxy` take `EasyProxyOptions`, not `CommandLineOptions`, for their proxy settings.
- [x] Tests that built `CommandLineOptions` only to drive one of those members now build the record; tests through the whole command line stay as they are.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.
- [x] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- Plan: every named member now takes `EasyProxyOptions` (ADR-0536). `CreateTransports`, `CreateTcpConnector`, `CreatePoolingConnector` and `OptionGroupConnectionSettings.Of` gained an `EasyProxyOptions` parameter beside the network and TLS records; `TransferContextFactory.Create` one after `EasyProtocolOptions`; `TransferPerformer.TrySelectProxyAndCredentials` one after `CommandLineOptions`. `Socks5AuthenticationMapping.FromCommandLine` became `Of(EasyProxyOptions, NegotiateOptions, ...)`, since it no longer reads the command line.
- `ProxyType` maps to the kind of proxy text with no scheme; `Https2` maps to HTTP (what `-x` + `--proxy-http2` always was) and sets `HttpProxyTunnelOptions.ProxyHttp2`. `--proxy-http2` with an environment proxy, a `--socks*` proxy or `--proxy1.0` no longer reaches the tunnel, as curl's tool sets `CURLOPT_PROXYTYPE` only with `-x`; no test pinned the old reading.
- Kept beside the record, as the task says: `-U` (a `NetworkCredential` into `TrySelect`), `--proxy-service-name` and `--delegation` (`NegotiateOptions`), and `--unix-socket` as a flag. `ConnectToTunnelsThroughProxy` reads only `--connect-to`, a network setting BL-2343 retargets, so it keeps `CommandLineOptions`. `HttpRequestOptionsMapping`'s `--proxy-header` copy is not a named member and is left for the HTTP group.
- Tests: call sites that built `CommandLineOptions` for `CreateTransports`/`CreateTcpConnector`/`Create` now also pass `EasyOptionsFromCommandLine.ProxyOf` (or `new EasyProxyOptions()` where no proxy option is given); `PreProxyOf`, `HaproxyProtocolOf` and the SOCKS5 mapping tests go through the record. New `Curl.Engine.UnitTests/EasyProxyOptionsReadingTests` (20 cases) drive each member from a record built directly.
- Measured: `dotnet build` clean; fast tests: no project failed; `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console`: 0 failing members. `PassingUpstreamCases.txt`, `CurlEasyErrorText.cs` and `InProcessCurl.RunAsync` untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. The engine's proxy classes read EasyProxyOptions; Curl.Console hands them EasyOptionsFromCommandLine.ProxyOf (ADR-0536)
