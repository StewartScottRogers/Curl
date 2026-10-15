---
id: BL-2324
title: Make the engine's TLS classes read EasyTlsOptions instead of CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2304, BL-2323]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Engine.IntegrationTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Api.UnitLibrary/Options/Tls, Curl.Api.UnitTests/Options/Tls]
requirement: none
created: 2026-10-15
completed:
---
# BL-2324 — Make the engine's TLS classes read EasyTlsOptions instead of CommandLineOptions

## Goal

The engine's TLS classes read `EasyTlsOptions` instead of `CommandLineOptions`, and `Curl.Console` hands them the record `EasyOptionsFromCommandLine.TlsOf` builds; behaviour is byte for byte unchanged.

## Context

ADR-0506 plan phase 1 step 4, split out of BL-2304 (which built `EasyOptionsFromCommandLine.TlsOf` and settled the record's gaps in ADR-0514). Engine classes to retarget: `TlsClientOptionsMapping` (`FromCommandLine`, `ProxyFromCommandLine`, `DohFromCommandLine`), `TlsBuildInformation`, `EngineComposition.CreateTlsProvider` and the TLS session cache wiring, and `TransferContextFactory`'s TLS fields. Follow the retarget rules in BL-2303's Context. Depends on BL-2323, which gives `Curl.Engine.UnitLibrary` its reference to `Curl.Api.UnitLibrary`.

Points ADR-0514 settles that this retarget must honour:
- `TlsPeerOptions.Certificate` and `KeyPassword` arrive already split from `-E` (`ClientCertificateArgument.Split`), while `Curl.Networking`'s `ClientCertificateLoader` splits `TlsClientOptions.ClientCertificate` itself today. Hand the loader the split path and pass phrase (so an escaped `\:` is not unescaped twice), which is why `Curl.Networking.UnitLibrary` is in `touches`.
- `SslVersion.Default` maps to `TlsVersion.SystemDefault`; `Proxy.SslVersion` `Tls1` is `--proxy-tlsv1`.
- `--ssl-sessions` (`SslSessionsFile`) and `--no-alpn` (`UseAlpn`) are not on `EasyTlsOptions`: keep passing them beside the record until their own groups move them.
- `--ca-native` and `--proxy-ca-native` still map to nothing in `TlsClientOptions` (ADR-0124).

## Acceptance criteria

- [ ] `TlsClientOptionsMapping`, `TlsBuildInformation`, `EngineComposition.CreateTlsProvider` and `TransferContextFactory`'s TLS fields take `EasyTlsOptions`, not `CommandLineOptions`, for their TLS settings.
- [ ] Tests that built `CommandLineOptions` only to drive one of those members now build the record; tests through the whole command line stay as they are.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console,Curl.Networking.UnitLibrary` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.
- [ ] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
