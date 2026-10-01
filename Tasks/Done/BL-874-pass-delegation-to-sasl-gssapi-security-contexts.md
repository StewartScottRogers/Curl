---
id: BL-874
title: Pass --delegation to SASL GSSAPI security contexts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-631, BL-852]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0188-service-names-and-delegation-reach-every-security-context-and-sspi-never-delegates.md]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-874 — Pass --delegation to SASL GSSAPI security contexts

## Goal

A SASL GSSAPI exchange (SMTP, IMAP, POP3) asks its security context for the `--delegation` level, as curl's GSS-API build passes `CURLOPT_GSSAPI_DELEGATION` to `gss_init_sec_context` for SASL too.

## Context

- ADR-0188 (BL-631): HTTP Negotiate passes the level; `--service-name` already reaches SASL through `MailRequestOptions.ServiceName`.
- `SecurityContextSaslExchange` builds its `SecurityContextRequest` from `SaslRequest`, which carries no delegation level; `SaslRequest` and `MailRequestOptions` live in `Curl.Protocol.Abstractions`, so this touches the shared contract.
- On Windows the router already clears the level (SSPI never delegates), so only off-Windows behaviour changes.

## Acceptance criteria

- [x] `Curl.Authentication.UnitTests` pin the delegation level `SecurityContextSaslExchange` passes to the factory for `None`, `Policy` and `Always`.
- [x] A `Curl.Console.UnitTests` test shows `--delegation always` reaching the SASL request.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Decided (ADR-0188 rule 6, under Stewart's delegation): the level rides on a new
  `SaslAuthenticator.GssapiDelegation` init property, set by `CurlComposition.CreateSaslAuthenticator`
  from the option group's `NegotiateOptions.Delegation`, not on `SaslRequest`. The level is per
  option group, like the authenticator, and putting it on `SaslRequest` would have made the SMTP,
  POP3 and IMAP handlers (outside `touches`) copy a value they never read. So
  `Curl.Protocol.Abstractions` is unchanged. SASL NTLM never asks for delegation, as curl's
  NTLM never calls `gss_init_sec_context`.
- Amended ADR-0188 rather than writing a new ADR: it already covers delegation reaching each
  context and said "SASL passes no level yet (BL-874)". Added it to `touches`; no task in Doing
  names it.
- Tests: `SaslAuthenticatorSecurityContextTests.Begin_Gssapi_AsksTheContextForTheDelegationLevel`
  (None/Policy/Always), `Begin_NtlmWithDelegationAlways_NeverAsksTheContextToDelegate`, and
  `CurlCompositionSaslDelegationTests` runs SMTP `AUTH GSSAPI` through the production
  composition with no/`policy`/`always` `--delegation` and pins the Kerberos context request
  (exit 94 from a context with no credentials). No option changed, so `--ai-help` is unaffected.
- Measure-CodeQuality: Curl.Authentication.UnitLibrary and Curl.Console 100% line, 100% branch,
  0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. SASL GSSAPI asks its Kerberos context for the --delegation level
