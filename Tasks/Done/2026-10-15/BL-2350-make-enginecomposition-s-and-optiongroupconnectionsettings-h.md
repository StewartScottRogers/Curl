---
id: BL-2350
title: Make EngineComposition's and OptionGroupConnectionSettings' HTTP-version and ALPN reads take EasyHttpOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2345]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Engine.IntegrationTests]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2350 — Make EngineComposition's and OptionGroupConnectionSettings' HTTP-version and ALPN reads take EasyHttpOptions

## Goal

`EngineComposition.CreateTcpConnector` (its ALPN list, `HttpsConnectFirstAttemptVersion` and `HttpsConnectSecondAttemptVersion`) and `OptionGroupConnectionSettings.Of` (its `ApplicationProtocols`) read the HTTP version from `EasyHttpOptions.HttpVersion` instead of `CommandLineOptions.HttpVersion`, and `HttpVersionMapping.FromRequested` (the bridge BL-2345 left for them) is gone once BL-2346 has retargeted `AltSvcTransferCache`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2345 (ADR-0506 plan phase 1 step 4, ADR-0527): BL-2345 made `HttpVersionMapping` map `Curl.Api`'s `HttpVersion` and retargeted `HttpRequestOptionsMapping`, `RedirectPolicyMapping` and `TransferContextFactory`'s HTTP fields, but threading `EasyHttpOptions` through `CreateTransports`, `CreateTcpConnector`, `CreatePoolingConnector` and their about 120 test call sites did not fit its run. Follow BL-2323's and BL-2324's pattern (an `EasyHttpOptions http` parameter beside `network` and `tls`); `Curl.Console`'s `CurlComposition` builds it with `EasyOptionsFromCommandLine.HttpOf`. `HttpsConnectFirstAttemptVersionOf` should map `HttpVersion` too. `--no-alpn` (`SslEnableAlpn`) may move with it, replacing `options.UseAlpn` in `TlsClientOptionsMapping` calls (ADR-0530).

## Acceptance criteria

- [x] `CreateTcpConnector`, `HttpsConnectFirstAttemptVersionOf` and `OptionGroupConnectionSettings.Of` read the HTTP version from `EasyHttpOptions`, not `CommandLineOptions`.
- [x] `HttpVersionMapping.FromRequested` has no caller left outside `AltSvcTransferCache`'s (removed if BL-2346 is Done).
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.

## Notes

- `CreateTransports` (both overloads), `CreateTcpConnector`, `CreatePoolingConnector` and `OptionGroupConnectionSettings.Of` take an `EasyHttpOptions http` parameter just before `proxy` (BL-2323's and BL-2324's pattern); `CurlComposition` builds it with `EasyOptionsFromCommandLine.HttpOf`, and the 108 test call sites pass `EasyOptionsFromCommandLine.HttpOf(<same options>)` beside the other groups.
- `HttpsConnectFirstAttemptVersionOf` takes `Curl.Api`'s `HttpVersion` (`None` for no version option, still `h2`); the second attempt checks `HttpVersion.Http3`. Its DataRow test now passes `HttpVersion` values.
- `HttpVersionMapping.FromRequested` is removed: BL-2346 is Done and `AltSvcTransferCache` no longer called it. Its one test, `FromRequested_MapsEachVersionOptionAsHttpOfDoes`, went with it, since the method it tested is gone; `HttpOf`'s version mapping stays pinned row by row in `EasyOptionsFromCommandLineTests.Http`.
- `--no-alpn` (`options.UseAlpn`) was left on `CommandLineOptions`, as ADR-0530 keeps it with `--ssl-sessions`; the Context said it "may" move, and leaving it keeps this task to the HTTP version alone.
- Measured: `dotnet build` clean; fast tests exit 0 (Curl.Engine.UnitTests 718 passed, Curl.Console.UnitTests 2818 passed); `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console`: 0 failing members.
## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EngineComposition's transport factories and OptionGroupConnectionSettings read the HTTP version from EasyHttpOptions; FromRequested removed
