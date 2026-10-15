---
id: BL-2306
title: Retarget the engine's authentication settings from CommandLineOptions to EasyAuthenticationOptions
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-2305, BL-2297]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Api.UnitLibrary/Options/Authentication, Curl.Api.UnitTests/Options/Authentication]
requirement: none
created: 2026-10-15
completed:
---
# BL-2306 — Retarget the engine's authentication settings from CommandLineOptions to EasyAuthenticationOptions

## Goal

The engine reads its authentication settings from `EasyAuthenticationOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds that record; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4; follows BL-2305 and uses the rules for every retarget task written in BL-2303's Context. Engine classes reading authentication settings (as of 2026-10-15): `TransferCredentialLookup` (netrc), `NegotiateOptionsMapping`, `AwsSigV4HttpAuthenticator`, `HttpSignatureSetup`, `EngineComposition`'s authenticator and security-context factories, the credential choice in `TransferPerformer`. Command-line options: `-u`, `-n`, `--netrc-file`, `--netrc-optional`, `--basic`, `--digest`, `--ntlm`, `--ntlm-wb`, `--negotiate`, `--anyauth`, `--proxy-user` and the proxy auth twins, `--login-options`, `--sasl-authzid`, `--sasl-ir`, `--oauth2-bearer`, `--service-name`, `--proxy-service-name`, `--delegation`, `--krb`, `--aws-sigv4`, `--tlsauthtype`, `--tlsuser`, `--tlspassword` (and proxy twins), `--location-trusted`, `--disallow-username-in-url`, and Curl's HTTP message signature options.

## Acceptance criteria

- [ ] The engine classes named in Context take `EasyAuthenticationOptions`, not `CommandLineOptions`, for their authentication settings.
- [ ] `EasyOptionsFromCommandLine` maps every authentication command-line option named in Context, each with a test; Curl's HTTP message signature options, which have no `CURLOPT_*`, are carried by a Curl-specific property documented as such.
- [ ] `dotnet build` is clean (warnings are errors).
- [ ] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [ ] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [ ] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [ ] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

## Log

- 2026-10-15: Created.
