---
id: BL-2352
title: Make HttpRequestOptionsMapping, HttpSignatureSetup, MailRequestOptionsMapping and RedirectPolicyMapping read EasyAuthenticationOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2338]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests]
requirement: none
created: 2026-10-15
completed:
---
# BL-2352 — Make HttpRequestOptionsMapping, HttpSignatureSetup, MailRequestOptionsMapping and RedirectPolicyMapping read EasyAuthenticationOptions

## Goal

`HttpSignatureSetup.Of`, `HttpRequestOptionsMapping.Of` (auth methods, bearer token, AWS SigV4, HTTP signature), `MailRequestOptionsMapping.FromCommandLine` (login options, SASL, bearer, service name), `RedirectPolicyMapping.Of` (`--location-trusted`, `--disallow-username-in-url`) and `TransferContextFactory.Create`'s `-u` credentials read `EasyAuthenticationOptions` instead of `CommandLineOptions`; behaviour is byte for byte unchanged.

## Context

Split out of BL-2338 (ADR-0506 plan phase 1 step 4; mapping settled in ADR-0516). `TransferContextFactory.Create` gains an `EasyAuthenticationOptions` parameter beside `EasyProxyOptions`, which it hands to `HttpRequestOptionsMapping.Of` and `MailRequestOptionsMapping.FromCommandLine`; `CurlCommandRunner.RefusedHttpSignatureSetupAsync` passes `EasyOptionsFromCommandLine.AuthenticationOf(options)` to `HttpSignatureSetup.Of`. `HttpAuthentication` is `CURLAUTH_*` bits: `CURLAUTH_ANY` is every scheme the engine knows plus Bearer only when `XOAuth2Bearer` is set, exactly as `CommandLineOptions.AuthSchemes` gives it, and the `CURLAUTH_AWS_SIGV4` bit is ignored (signing reads `AwsSigV4`). `-u` becomes `UserPassword` split at the first colon into the `NetworkCredential` (`TransferCredentialLookup` already does that split since BL-2338). Follow the retarget rules in BL-2303's Context.

## Acceptance criteria

- [ ] The members named in the Goal take `EasyAuthenticationOptions`, not `CommandLineOptions`, for their authentication settings.
- [ ] Tests that built `CommandLineOptions` only to drive one of those members now build the record; tests through the whole command line stay as they are.
- [ ] `dotnet build` is clean and `dotnet test --filter "TestCategory!=Integration"` passes, no existing test deleted, skipped or weakened.
- [ ] `Measure-CodeQuality.ps1 -Library Curl.Engine.UnitLibrary,Curl.Console` passes.
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case still passes.

## Notes

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
