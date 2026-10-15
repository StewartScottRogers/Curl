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
completed: 2026-10-15
---
# BL-2306 — Retarget the engine's authentication settings from CommandLineOptions to EasyAuthenticationOptions

## Goal

The engine reads its authentication settings from `EasyAuthenticationOptions` instead of `CommandLineOptions`, and `EasyOptionsFromCommandLine` builds that record; behaviour is byte for byte unchanged.

## Context

ADR-0506; plan phase 1 step 4; follows BL-2305 and uses the rules for every retarget task written in BL-2303's Context. Engine classes reading authentication settings (as of 2026-10-15): `TransferCredentialLookup` (netrc), `NegotiateOptionsMapping`, `AwsSigV4HttpAuthenticator`, `HttpSignatureSetup`, `EngineComposition`'s authenticator and security-context factories, the credential choice in `TransferPerformer`. Command-line options: `-u`, `-n`, `--netrc-file`, `--netrc-optional`, `--basic`, `--digest`, `--ntlm`, `--ntlm-wb`, `--negotiate`, `--anyauth`, `--proxy-user` and the proxy auth twins, `--login-options`, `--sasl-authzid`, `--sasl-ir`, `--oauth2-bearer`, `--service-name`, `--proxy-service-name`, `--delegation`, `--krb`, `--aws-sigv4`, `--tlsauthtype`, `--tlsuser`, `--tlspassword` (and proxy twins), `--location-trusted`, `--disallow-username-in-url`, and Curl's HTTP message signature options.

## Acceptance criteria

- [x] The engine classes named in Context take `EasyAuthenticationOptions`, not `CommandLineOptions`, for their authentication settings. (Split to BL-2338; see Notes.)
- [x] `EasyOptionsFromCommandLine` maps every authentication command-line option named in Context, each with a test; Curl's HTTP message signature options, which have no `CURLOPT_*`, are carried by a Curl-specific property documented as such.
- [x] `dotnet build` is clean (warnings are errors).
- [x] `dotnet test --filter "TestCategory!=Integration"` passes and no existing test was deleted, skipped or weakened.
- [x] `powershell -NoProfile -File Measure-CodeQuality.ps1` passes (100% line and branch coverage, complexity <= 10, CRAP <= 30).
- [x] `Curl.Conformance.UnitTests/PassingUpstreamCases.txt` is unchanged and every case it lists still passes (the ratchet is unchanged).
- [x] The gap office's scores are unchanged: `Curl.Console/CurlEasyErrorText.cs` stays at that path and `InProcessCurl.RunAsync`'s public signatures are unchanged.

## Notes

- 2026-10-15, split (rule 4, before any code): `Curl.Engine.UnitLibrary` has no reference to `Curl.Api.UnitLibrary` until BL-2323 (still in Backlog), so the engine classes cannot take `EasyAuthenticationOptions` in this run, as BL-2303 to BL-2305 found for their groups. This task delivers the mapper (`EasyOptionsFromCommandLine.AuthenticationOf`) and ADR-0516; BL-2338 (depends on this and BL-2323) retargets the engine's authentication classes.
- Decided (ADR-0516, by Claude under Stewart's delegation): the methods are the engine's `AuthSchemes`/`ProxyAuthSchemes` as `CURLAUTH_*` bits, every known scheme being `CURLAUTH_ANY`, plus `CURLAUTH_AWS_SIGV4` with `--aws-sigv4` (differs from `--libcurl` only in bits the engine never uses); `-u`/`-U` are `CURLOPT_USERPWD`/`PROXYUSERPWD` as `user:password`; `--ntlm-wb`, `--krb` and the TLS-SRP options have no function in the reference build and keep libcurl's defaults.
- `EasyAuthenticationOptions` gained four Curl-specific members, `HttpSignatureAlgorithm`, `HttpSignatureKey` (a secret, redacted by `ToString`), `HttpSignatureKeyId` and `HttpSignatureHeaders`, each documented as having no `CURLOPT_*`, with default, `with` and redaction tests.
- No command-line option changed, so `--ai-help` needs nothing.
- Measured: `Measure-CodeQuality.ps1 -Library Curl.Console,Curl.Api.UnitLibrary` - both 100% line and branch, 0 failing members, worst CRAP 10 (Console) and 2 (Api). Fast tests: 39 assemblies green (Console 2543 passed, 25 skipped; Api 422 passed). Ratchet file unchanged.

## Log

- 2026-10-15: Created.
- 2026-10-15: Backlog -> Doing.
- 2026-10-15: Doing -> Done. EasyOptionsFromCommandLine.AuthenticationOf maps every authentication option onto EasyAuthenticationOptions (ADR-0516); engine retarget split to BL-2338
