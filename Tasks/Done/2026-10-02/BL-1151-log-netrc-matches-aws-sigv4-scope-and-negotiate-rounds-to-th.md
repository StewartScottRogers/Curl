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
completed: 2026-10-02
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

- [x] `Curl.Authentication.UnitTests` pin a netrc match at `info` that contains the host and login and not the password, the SigV4 scope at `info` without the secret key or signature, and a Negotiate round at `verbose` without the token's base64.
- [x] `Curl.Console/CurlComposition.cs` passes the run's log to each of them that now takes one.
- [x] Every existing test in the touched test projects passes unmodified.
- [x] `dotnet build Curl.slnx -warnaserror` is clean and `dotnet test --filter "TestCategory!=Integration"` passes.
- [x] `Measure-CodeQuality.ps1 -Library <library>` reports 100% line and branch coverage and no failing member for each library touched.

## Notes

- Each of the four takes an optional `IDiagnosticLog? diagnosticLog = null` as its last parameter (constructor, or `NetrcFile.Find`'s last argument), so every existing caller and test compiles unchanged. `NetrcFile` stays static: the log is a parameter of `Find`, which is the simplest of the two options the Context offered and keeps the netrc test in `Curl.Authentication.UnitTests`.
- Lines: `netrc entry matched for host <host>, login <login|(none)>` and `AWS SigV4 scope: provider <p0>:<p1>, region <r>, service <s>` at `info`; `Negotiate needs another round` (a ContinueNeeded step with a token) and `Negotiate completed` at `verbose`; `Negotiate context failed: <SecurityContextStatus>` at `warning`, from both `StepAsync` and `StepWithoutAnsweringAsync`; `<mechanism> context for <host>: hand-built NTLM|hand-built Kerberos (<why>)` at `verbose` from `HandBuiltSecurityContextFactory`. The hand-built SPNEGO only ever offers Kerberos V5, so the "Kerberos-or-NTLM choice" it logs is that one, said as "SPNEGO offering Kerberos V5 alone, not NTLM".
- `AuthDiagnosticLog`'s remark used to say "never a user name"; the netrc login is now the one exception the task asks for, and the remark says so.
- Wiring: `CurlComposition` passes the run's log to `NegotiateHttpAuthenticator`, `HandBuiltSecurityContextFactory` and `AwsSigV4Signer`; netrc is looked up by `TransferCredentialLookup`, built by `CurlCommandRunner.CredentialLookup`, which now passes the runner's `diagnosticLog`. Pinned end to end by `CurlCommandRunnerDiagnosticLogTests.RunAsync_NetrcFileAndAwsSigV4AtLogLevelInfo_LogsTheNetrcMatchAndTheScopeButNotThePassword`.
- `StepAsync` reached cyclomatic complexity 12 with the round logging inline, so it moved to `LogRound`.
- One `Measure-CodeQuality.ps1 -Library Curl.Console` run failed on a Conformance test under coverage load; the rerun and the plain fast run passed it (not touched by this task).
- Results: `Curl.Authentication.UnitLibrary` and `Curl.Console` 100% line and branch, 0 failing members.

## Log

- 2026-10-01: Created.
- 2026-10-02: Backlog -> Doing.
- 2026-10-02: Doing -> Done. Netrc matches, AWS SigV4 scope, Negotiate rounds and failures, and the hand-built context chosen are written to the auth diagnostic log, never a secret
