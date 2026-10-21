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
completed: 2026-10-15
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

- [x] `TlsClientOptionsMapping`, `TlsBuildInformation`, `EngineComposition.CreateTlsProvider` and `TransferContextFactory`'s TLS fields take `EasyTlsOptions`, not `CommandLineOptions`, for their TLS settings.
- [x] Tests that built `CommandLineOptions` only to drive one of those members now build the record; tests through the whole command line stay as they are.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console,Curl.Networking.UnitLibrary` passes.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.
- [x] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- Plan: `TlsClientOptionsMapping.Of/ProxyOf/DohOf` map `EasyTlsOptions` (with `UseAlpn` and `SslSessionsFile` beside it); `CreateTransports`, `CreateDnsResolver`, `CreateDohConnector`, `CreatePoolingConnector` and `OptionGroupConnectionSettings.Of` take the record; `TlsBuildInformation.Lines(EasyTlsOptions, bool caEmbedDumpRequested)`. ADR-0530.
- `TransferContextFactory` had no TLS fields; nothing to move there.
- `ClientCertificateLoader` no longer splits `-E`; Networking tests now split their `file:pass` values (test helper) before the loader.
- `TlsPeerOptions.CertificateType`/`KeyType` are nullable (Schannel reads no type as P12 but refuses `PEM`); the `pkcs11:` `ENG` guess was dropped to keep behaviour unchanged, filed as BL-2349.
- Measured: fast tests green in every project (Engine 654, Console 2798, Networking 3175, Api 423, Conformance 3129 passed); Measure-CodeQuality for Engine, Console, Networking and Api: 0 failing members. PassingUpstreamCases.txt unchanged; CurlEasyErrorText.cs and InProcessCurl untouched.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. The engine's TLS classes read EasyTlsOptions built by EasyOptionsFromCommandLine.TlsOf; -E is split once; behaviour unchanged (ADR-0530)
