---
id: BL-2338
title: Make the engine's authentication classes read EasyAuthenticationOptions instead of CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2306, BL-2323]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Engine.IntegrationTests, Curl.Api.UnitLibrary/Options/Authentication, Curl.Api.UnitTests/Options/Authentication]
requirement: none
created: 2026-10-15
completed: 2026-10-15
---
# BL-2338 — Make the engine's authentication classes read EasyAuthenticationOptions instead of CommandLineOptions

## Goal

The engine's authentication classes read `EasyAuthenticationOptions` instead of `CommandLineOptions`, and `Curl.Console` hands them the record `EasyOptionsFromCommandLine.AuthenticationOf` builds; behaviour is byte for byte unchanged.

## Context

ADR-0506 plan phase 1 step 4, split out of BL-2306 (which built `EasyOptionsFromCommandLine.AuthenticationOf` and settled the mapping in ADR-0516). Engine classes to retarget: `TransferCredentialLookup` (netrc), `NegotiateOptionsMapping`, `AwsSigV4HttpAuthenticator`, `HttpSignatureSetup`, `EngineComposition`'s authenticator and security-context factories, and the credential choice in `TransferPerformer`. Follow the retarget rules in BL-2303's Context. Depends on BL-2323, which gives `Curl.Engine.UnitLibrary` its reference to `Curl.Api.UnitLibrary`.

Points ADR-0516 settles that this retarget must honour:
- `HttpAuthentication`/`ProxyAuthentication` are the engine's `AuthSchemes`/`ProxyAuthSchemes` as `CURLAUTH_*` bits; `CURLAUTH_ANY` is every scheme the engine knows (Basic, Digest, NTLM, Negotiate) plus Bearer only when `XOAuth2Bearer` is set, as `AuthSchemes` gives it today. `CURLAUTH_AWS_SIGV4` is set whenever `AwsSigV4` is; the engine's AWS signing reads `AwsSigV4`, not the bit.
- `UserPassword`/`ProxyUserPassword` are `user:password` split at the first colon, as `CommandLineOptions.Credentials` splits `-u`.
- `UnrestrictedAuthentication` is `--location-trusted`; `HttpSignature*` carry the `--httpsig-*` options.

## Acceptance criteria

- [x] `TransferCredentialLookup` and `TransferPerformer`'s credential choice (`TrySelectProxyAndCredentials`) take `EasyAuthenticationOptions`, not `CommandLineOptions`, for their authentication settings. `NegotiateOptionsMapping` and `EngineComposition`'s transport factories moved to BL-2351; `HttpSignatureSetup` (and its caller `HttpRequestOptionsMapping`) to BL-2352; `AwsSigV4HttpAuthenticator` and `EngineComposition`'s authenticator and security-context factories (`CreateHttpAuthenticator`, `CreateSecurityContextFactory`, `CreateDialingSecurityContextFactory`, `CreateSaslAuthenticator`) read no `CommandLineOptions` at all, so nothing changes there (see Notes).
- [x] Tests that built `CommandLineOptions` only to drive one of those members now build the record (`EasyAuthenticationOptionsReadingTests`); tests through the whole command line stay as they are.
- [x] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.
- [x] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- 2026-10-15 (lane 3): Split before any edit. The scope pass found that `NegotiateOptionsMapping`'s engine callers (`CreateTcpConnector` for SOCKS5 GSS-API, `CreateProxyTunnelOptions`) cannot get the record without threading it through `CreateTransports`, `CreateTcpConnector`, `CreatePoolingConnector` and `CreateProxyTunnelOptions` (about 100 test call sites, as BL-2325 did for `ProxyOf`), and `HttpSignatureSetup.Of`'s engine caller `HttpRequestOptionsMapping.Of` cannot get it without adding the record to `TransferContextFactory.Create` (about 35 test call sites). Too much for one run beside the lookup, so filed as BL-2351 and BL-2352, both now in BL-2310's `depends-on`.
- `AwsSigV4HttpAuthenticator` reads only `HttpAuthRequest.AwsSigV4`; the `--aws-sigv4` value reaches it through `HttpRequestOptionsMapping` (BL-2352). `EngineComposition`'s `CreateHttpAuthenticator`, `CreateSecurityContextFactory`, `CreateDialingSecurityContextFactory` and `CreateSaslAuthenticator` already take only `NegotiateOptions`, `SecurityDelegation` and their connectors.
- Choice: `-u` wins when `EasyAuthenticationOptions.UserName` is set, else when `UserPassword` up to its first colon is not empty, matching libcurl's `CURLOPT_USERNAME` taking precedence over `CURLOPT_USERPWD`; the curl tool sets only `UserPassword`, so command-line behaviour is unchanged. Covered by ADR-0516's mapping; no new ADR.
- Measured: fast tests exit 0 (Engine 716, Console 2826, Conformance 3129 passed); quality report: Curl.Console and Curl.Engine.UnitLibrary 100% line, 100% branch, max complexity 10, 0 failing members.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. TransferCredentialLookup and TrySelectProxyAndCredentials read EasyAuthenticationOptions; Negotiate/transport factories split to BL-2351, HttpSignatureSetup/request mappings to BL-2352
