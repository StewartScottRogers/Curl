---
id: BL-2326
title: Make the engine's authentication classes read EasyAuthenticationOptions instead of CommandLineOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2306, BL-2323]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Engine.IntegrationTests, Curl.Api.UnitLibrary/Options/Authentication, Curl.Api.UnitTests/Options/Authentication]
requirement: none
created: 2026-10-15
completed:
---
# BL-2326 — Make the engine's authentication classes read EasyAuthenticationOptions instead of CommandLineOptions

## Goal

The engine's authentication classes read `EasyAuthenticationOptions` instead of `CommandLineOptions`, and `Curl.Console` hands them the record `EasyOptionsFromCommandLine.AuthenticationOf` builds; behaviour is byte for byte unchanged.

## Context

ADR-0506 plan phase 1 step 4, split out of BL-2306 (which built `EasyOptionsFromCommandLine.AuthenticationOf` and settled the mapping in ADR-0516). Engine classes to retarget: `TransferCredentialLookup` (netrc), `NegotiateOptionsMapping`, `AwsSigV4HttpAuthenticator`, `HttpSignatureSetup`, `EngineComposition`'s authenticator and security-context factories, and the credential choice in `TransferPerformer`. Follow the retarget rules in BL-2303's Context. Depends on BL-2323, which gives `Curl.Engine.UnitLibrary` its reference to `Curl.Api.UnitLibrary`.

Points ADR-0516 settles that this retarget must honour:
- `HttpAuthentication`/`ProxyAuthentication` are the engine's `AuthSchemes`/`ProxyAuthSchemes` as `CURLAUTH_*` bits; `CURLAUTH_ANY` is every scheme the engine knows (Basic, Digest, NTLM, Negotiate) plus Bearer only when `XOAuth2Bearer` is set, as `AuthSchemes` gives it today. `CURLAUTH_AWS_SIGV4` is set whenever `AwsSigV4` is; the engine's AWS signing reads `AwsSigV4`, not the bit.
- `UserPassword`/`ProxyUserPassword` are `user:password` split at the first colon, as `CommandLineOptions.Credentials` splits `-u`.
- `UnrestrictedAuthentication` is `--location-trusted`; `HttpSignature*` carry the `--httpsig-*` options.

## Acceptance criteria

- [ ] `TransferCredentialLookup`, `NegotiateOptionsMapping`, `AwsSigV4HttpAuthenticator`, `HttpSignatureSetup`, `EngineComposition`'s authenticator and security-context factories and `TransferPerformer`'s credential choice take `EasyAuthenticationOptions`, not `CommandLineOptions`, for their authentication settings.
- [ ] Tests that built `CommandLineOptions` only to drive one of those members now build the record; tests through the whole command line stay as they are.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.
- [ ] `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
