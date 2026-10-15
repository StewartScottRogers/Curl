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
completed:
---
# BL-2350 — Make EngineComposition's and OptionGroupConnectionSettings' HTTP-version and ALPN reads take EasyHttpOptions

## Goal

`EngineComposition.CreateTcpConnector` (its ALPN list, `HttpsConnectFirstAttemptVersion` and `HttpsConnectSecondAttemptVersion`) and `OptionGroupConnectionSettings.Of` (its `ApplicationProtocols`) read the HTTP version from `EasyHttpOptions.HttpVersion` instead of `CommandLineOptions.HttpVersion`, and `HttpVersionMapping.FromRequested` (the bridge BL-2345 left for them) is gone once BL-2346 has retargeted `AltSvcTransferCache`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2345 (ADR-0506 plan phase 1 step 4, ADR-0527): BL-2345 made `HttpVersionMapping` map `Curl.Api`'s `HttpVersion` and retargeted `HttpRequestOptionsMapping`, `RedirectPolicyMapping` and `TransferContextFactory`'s HTTP fields, but threading `EasyHttpOptions` through `CreateTransports`, `CreateTcpConnector`, `CreatePoolingConnector` and their about 120 test call sites did not fit its run. Follow BL-2323's and BL-2324's pattern (an `EasyHttpOptions http` parameter beside `network` and `tls`); `Curl.Console`'s `CurlComposition` builds it with `EasyOptionsFromCommandLine.HttpOf`. `HttpsConnectFirstAttemptVersionOf` should map `HttpVersion` too. `--no-alpn` (`SslEnableAlpn`) may move with it, replacing `options.UseAlpn` in `TlsClientOptionsMapping` calls (ADR-0530).

## Acceptance criteria

- [ ] `CreateTcpConnector`, `HttpsConnectFirstAttemptVersionOf` and `OptionGroupConnectionSettings.Of` read the HTTP version from `EasyHttpOptions`, not `CommandLineOptions`.
- [ ] `HttpVersionMapping.FromRequested` has no caller left outside `AltSvcTransferCache`'s (removed if BL-2346 is Done).
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.

## Notes

## Log

- 2026-10-15: Created.
