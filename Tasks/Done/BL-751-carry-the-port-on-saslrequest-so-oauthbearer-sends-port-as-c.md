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
completed: 2026-09-29
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

- [x] `SaslRequest` has an `int Port` member documented as the connection's port (the URL's, or the scheme's default), pinned in `Curl.Protocol.Abstractions.UnitTests`.
- [x] `SaslAuthenticatorTests` pins `Begin("OAUTHBEARER", ...)` with port 18125 to `bixhPXUsAWhvc3Q9MTI3LjAuMC4xAXBvcnQ9MTgxMjUBYXV0aD1CZWFyZXIgdG9rAQE=` for user `u`, host `127.0.0.1`, token `tok`.
- [x] The XML docs of `ISaslExchange.InitialResponse` and `ISaslExchange.Respond` state ADR-0123 point 4.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary` and `-Library Curl.Protocol.Abstractions.UnitLibrary` report 100% line and branch coverage and no failing member.

## Notes

- From BL-541 (2026-09-28): `ISaslExchange.Respond`'s XML doc says a `null` answer makes
  the handler cancel with `*`. The SMTP handler sends nothing and fails with 67, as curl
  does for the built mechanisms (ADR-0133 decision 6). Reword that doc too: a `null`
  answer is exit 67, and whether a cancel line is sent is each handler's decision.
- The SMTP handler builds its `SaslRequest` in
  `Curl.Protocol.Smtp.UnitLibrary/SmtpSaslAuthentication.cs` (`CreateRequest`). Adding a
  required `Port` breaks that call, so this task also touches `Curl.Protocol.Smtp.UnitLibrary`
  unless the member gets a default.
- 2026-09-29 (Claude): `Port` has a default of `0` rather than being required, so the SMTP, IMAP and POP3 callers still compile and this task stays inside its `touches`; `0` leaves `port=` out, as curl does for port 0. Wiring the handlers to pass the real port is BL-875.
- 2026-09-29: Resumed from lane 3's cherry-picked work; build clean and all fast tests green after rebasing. Both libraries measure 100% line and branch coverage, 0 failing members.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Backlog. Lane 3 could not integrate: fast tests failed after rebasing onto the other lanes' work. The work is on branch factory/BL-751-lane-3-20260928-212155; start with git cherry-pick --no-commit factory/BL-751-lane-3-20260928-212155 and fix it.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. SaslRequest carries the port and OAUTHBEARER sends port= as curl does
