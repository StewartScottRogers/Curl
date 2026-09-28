---
id: BL-751
title: Carry the port on SaslRequest so OAUTHBEARER sends port= as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-536]
touches: [Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-09-28
completed:
---
# BL-751 — Carry the port on SaslRequest so OAUTHBEARER sends port= as curl does

## Goal

`SaslRequest` carries the connection's port, `SaslAuthenticator.Begin("OAUTHBEARER", ...)` sends `n,a=<u>,^Ahost=<h>^Aport=<p>^Aauth=Bearer <t>^A^A` as curl 8.21.0 does, and the Abstractions XML documentation matches ADR-0123 (LOGIN has an initial response; an initial response not sent inline answers the first challenge).

## Context

- ADR-0123 points 4 and 6 (BL-536). curl always sends `port=` (measured `port=18125` against `smtp://127.0.0.1:18125/x`); `SaslRequest` has no port today, so `Begin` leaves the field out.
- `SaslAuthenticator.OAuthBearerMessage(user, host, port, token)` already takes the port and is pinned with it in `SaslAuthenticatorTests.OAuthBearerMessage_WithPort_MatchesCurl`.
- `ISaslExchange.InitialResponse`'s XML doc lists LOGIN as having none; ADR-0123 point 4 says LOGIN's is the user name. `ISaslAuthenticator`/`ISaslExchange` docs should state the RFC 4422 §5 convention.
- Adding a positional member to the record changes every `new SaslRequest(...)`; check the callers (BL-539 and the mail handlers, if landed).

## Acceptance criteria

- [ ] `SaslRequest` has an `int Port` member documented as the connection's port (the URL's, or the scheme's default), pinned in `Curl.Protocol.Abstractions.UnitTests`.
- [ ] `SaslAuthenticatorTests` pins `Begin("OAUTHBEARER", ...)` with port 18125 to `bixhPXUsAWhvc3Q9MTI3LjAuMC4xAXBvcnQ9MTgxMjUBYXV0aD1CZWFyZXIgdG9rAQE=` for user `u`, host `127.0.0.1`, token `tok`.
- [ ] The XML docs of `ISaslExchange.InitialResponse` and `ISaslExchange.Respond` state ADR-0123 point 4.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` and `-Library Curl.Protocol.Abstractions.UnitLibrary` report 100% line and branch coverage and no failing member.

## Notes

## Log

- 2026-09-28: Created.
