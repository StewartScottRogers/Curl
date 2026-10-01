---
id: BL-874
title: Pass --delegation to SASL GSSAPI security contexts
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-631, BL-852]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Console, Curl.Console.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-874 — Pass --delegation to SASL GSSAPI security contexts

## Goal

A SASL GSSAPI exchange (SMTP, IMAP, POP3) asks its security context for the `--delegation` level, as curl's GSS-API build passes `CURLOPT_GSSAPI_DELEGATION` to `gss_init_sec_context` for SASL too.

## Context

- ADR-0188 (BL-631): HTTP Negotiate passes the level; `--service-name` already reaches SASL through `MailRequestOptions.ServiceName`.
- `SecurityContextSaslExchange` builds its `SecurityContextRequest` from `SaslRequest`, which carries no delegation level; `SaslRequest` and `MailRequestOptions` live in `Curl.Protocol.Abstractions`, so this touches the shared contract.
- On Windows the router already clears the level (SSPI never delegates), so only off-Windows behaviour changes.

## Acceptance criteria

- [ ] `Curl.Authentication.UnitTests` pin the delegation level `SecurityContextSaslExchange` passes to the factory for `None`, `Policy` and `Always`.
- [ ] A `Curl.Console.UnitTests` test shows `--delegation always` reaching the SASL request.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
