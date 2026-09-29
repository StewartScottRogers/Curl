---
id: BL-923
title: Log HTTP and SASL authentication scheme choices to the diagnostic log in Curl.Authentication
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-916, BL-919]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-923 — Log HTTP and SASL authentication scheme choices to the diagnostic log in Curl.Authentication

## Goal

`Curl.Authentication.UnitLibrary` writes the diagnostic log (component `auth`) for which HTTP scheme and which SASL mechanism was chosen and why, each round of a multi-round scheme, and each refusal, never a credential or token byte.

## Context

- The rules are BL-915's ADR: levels (decision 2), components (6), never-logged values (7), and "test `IsEnabled` before building a message" (8). The contract is BL-916's `IDiagnosticLog` and `DiagnosticLogComponents`.
- This task changes no `-v`, `--trace`, standard output or exit-code behaviour: every existing test in the touched test projects passes unmodified.
- Tests use a hand-rolled `RecordingDiagnosticLog : IDiagnosticLog` in each touched test project (no mocking library), recording `(level, component, message)` at a configurable level. Tests are platform-neutral.
- Where: `HttpAuthSchemeRanking.cs`, `RankedHttpAuthenticator.cs`, `BasicAndBearerAuthenticator.cs`, `DigestAuthenticator.cs`, `NtlmHttpAuthenticator.cs`, `NegotiateHttpAuthenticator.cs`, `SaslMechanismRanking.cs`, `SaslAuthenticator.cs`, `RoutingSecurityContextFactory.cs`/`HandBuiltSecurityContextFactory.cs` (hand-built or system context, and why), `NetrcFile.cs` (which netrc entry matched, by host and login only), `AwsSigV4Signer.cs` (the scope, never the key). `Curl.Ntlm` and `Curl.Kerberos` get no project reference to the abstractions: log around their calls here.
- These services are built in `Curl.Console/CurlComposition.cs` (`CreateHttpAuthenticator`, `CreateSaslAuthenticator`, `CreateSecurityContextFactory`): they take `IDiagnosticLog` through their constructor and the composition root passes the composed log. That is why this task also touches `Curl.Console` and waits for BL-919.
- What, per level: `error` authentication that ends the transfer (for example exit 67, `LoginDenied`) with the reason; `warning` a scheme or mechanism skipped because it failed or is unavailable; `info` the scheme or mechanism chosen and the schemes the server offered; `verbose` each round (`NTLM type 1 sent`, `Negotiate needs another round`), the Digest algorithm and qop chosen.
- Credential-bearing paths: Basic, Digest, NTLM and SASL PLAIN with the password `s3cret`; no recorded message contains it or the base64 of any token.

## Acceptance criteria

- [ ] `Curl.Authentication.UnitTests` pin: Digest chosen over Basic when both are offered, at `info`; an NTLM round at `verbose`; a SASL mechanism chosen from a server's list at `info`; a refused login at `error`; and the no-secret tests above.
- [ ] `Curl.Console/CurlComposition.cs` passes the composed log to every authenticator and factory that now takes one.
- [ ] With the default `NoDiagnosticLog.Instance` every existing test in the touched test projects passes unmodified.
- [ ] A test shows that at `DiagnosticLogLevel.Error` no `info` or `verbose` line is recorded, and one test per credential-bearing path listed in Context shows no recorded message contains the secret.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched (complexity at most 10 per method: put logging in small helpers rather than growing a method).

## Notes

## Log

- 2026-09-29: Created.
