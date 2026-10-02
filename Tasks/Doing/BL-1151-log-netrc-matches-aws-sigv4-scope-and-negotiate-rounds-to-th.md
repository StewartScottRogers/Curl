---
id: BL-1151
title: Log netrc matches, AWS SigV4 scope and Negotiate rounds to the diagnostic log in Curl.Authentication
priority: Low
assignee: Claude
pipeline: feature
depends-on: [BL-923]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-10-01
completed:
---
# BL-1151 — Log netrc matches, AWS SigV4 scope and Negotiate rounds to the diagnostic log in Curl.Authentication

## Goal

`NetrcFile`, `AwsSigV4Signer`, `NegotiateHttpAuthenticator` and `HandBuiltSecurityContextFactory` write their choices to the diagnostic log (component `auth`) through BL-923's `AuthDiagnosticLog`, never a password, key, signature or token byte.

## Context

- BL-923 added `Curl.Authentication.UnitLibrary/AuthDiagnosticLog.cs` and wired it into `RankedHttpAuthenticator`, `DigestAuthenticator`, `NtlmHttpAuthenticator`, `SaslAuthenticator` and `RoutingSecurityContextFactory`; `Curl.Console/CurlComposition.cs` passes the run's `LateBoundDiagnosticLog` to them. These four were left out of BL-923 to keep it one run.
- What, per level (ADR-0222): `info` which netrc entry matched, by host and login only, and the SigV4 scope (provider, region, service; never the key or signature); `verbose` each Negotiate round (`Negotiate needs another round`, `Negotiate completed`) and the Kerberos-or-NTLM choice inside the hand-built SPNEGO; `warning` a Negotiate context that fails, with its `SecurityContextStatus`.
- `NetrcFile` is static today: take the log as a parameter of its lookup, or log around the call in `Curl.Console`, whichever keeps it simple.
- Tests use `Curl.Authentication.UnitTests/Fakes/RecordingDiagnosticLog.cs`.

## Acceptance criteria

- [ ] `Curl.Authentication.UnitTests` pin a netrc match at `info` that contains the host and login and not the password, the SigV4 scope at `info` without the secret key or signature, and a Negotiate round at `verbose` without the token's base64.
- [ ] `Curl.Console/CurlComposition.cs` passes the run's log to each of them that now takes one.
- [ ] Every existing test in the touched test projects passes unmodified.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [ ] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
