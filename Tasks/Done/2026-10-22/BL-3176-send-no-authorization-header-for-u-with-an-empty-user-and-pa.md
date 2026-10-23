---
id: BL-3176
title: Send no Authorization header for -u : with an empty user and password
priority: Normal
assignee: Claude
pipeline: protocol
depends-on: []
touches: [Curl.Authentication.UnitLibrary/BasicAndBearerAuthenticator.cs, Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs, Curl.Authentication.UnitTests/BasicAuthEmptyCredentialsTests.cs, Curl.Authentication.UnitTests/AuthenticationAdversarialTests.cs, Curl.Protocol.Http.UnitTests/HttpProtocolHandlerEmptyCredentialsTests.cs]
requirement: none
created: 2026-10-22
completed: 2026-10-22
---
# BL-3176 — Send no Authorization header for -u : with an empty user and password

## Goal

`curl -u : http://host/` sends no `Authorization` header and prints no `Server auth` line, as upstream does.

## Context

Stewart reported on 2026-10-22: `curl -v -u : http://host/`. Upstream sends no Authorization header and no `* Server auth using Basic with user ''` line; Curl sends `Authorization: Basic Og==` (base64 of `:`). With an empty user and an empty password nothing is sent. Measure curl 8.22.0 with `Record-CurlExchange.ps1` for `-u :`, `-u ''`, `-u user:` (empty password: upstream does send `Basic dXNlcjo=`), `-u :pass` (empty user), and the proxy equivalent `-U :` (`--proxy-user`), and pin each. The Basic header is built in `Curl.Authentication.UnitLibrary/BasicAndBearerAuthenticator.cs` (line 41); the "using" verbose line is in `Curl.Protocol.Http.UnitLibrary/HttpAuthUsingLines.cs`; credentials reach them via `Curl.Engine.UnitLibrary/HttpRequestOptionsMapping.cs`. Do the check where one decision keeps both the header and the verbose line silent (the authenticator's applicability, not two separate checks).

## Acceptance criteria

- [x] `Curl.Authentication.UnitTests/BasicAuthEmptyCredentialsTests.cs` shows empty user and empty password give no Basic value, `user:` and `:pass` still do, with the measured values for curl 8.22.0 noted.
- [x] A wire-level test in `Curl.Protocol.Http.UnitTests/HttpProtocolHandlerEmptyCredentialsTests.cs` captures the request bytes for `-u :` and asserts there is no `Authorization` header line, and that `-v` output has no `Server auth` line.
- [x] The same test class pins `-U :` for the proxy-authorization header and its verbose line as curl 8.22.0 measures.
- [x] `dotnet build Curl.Authentication.UnitLibrary Curl.Protocol.Http.UnitLibrary -warnaserror` is clean and the fast tests of both `.UnitTests` projects pass.

## Notes

- Measured curl 8.22.0 (Git for Windows ucrt64) with `Record-CurlExchange.ps1 -sv` against a loopback server: `-u :` sends no Authorization header and no `Server auth` line; `-u user:` sends `Basic dXNlcjo=` with `Server auth using Basic with user 'user'`; `-u :pass` sends `Basic OnBhc3M=` with `... with user ''`; `-x ... -U :` sends no Proxy-Authorization and no `Proxy auth` line; `-U user:` sends `Proxy-Authorization: Basic dXNlcjo=` with `Proxy auth using Basic with user 'user'`; `-U :pass` sends `Basic OnBhc3M=`. `-u ''` (no colon) makes curl prompt for a password on stdin, so it is not a user:password pair and was not recorded (that run waited on the prompt).
- The one decision: `BasicAndBearerAuthenticator.CreateBasic` gives no value when both the user and the password are empty. With no value, `HttpAuthUsingLines` already writes no `auth using` line, so `HttpAuthUsingLines.cs` needed no change. Negotiate's `-u :` convention is untouched (only Basic changes). No ADR: this matches measured curl and leaves no design choice open.
- Added `Curl.Authentication.UnitTests/AuthenticationAdversarialTests.cs` to touches: its boundary row pinned the old `-u :` -> `Basic Og==`, which is now wrong; that row is removed (the case now lives in BasicAuthEmptyCredentialsTests). No task in Doing on origin/work/dark-factory names that file or project.
- `dotnet build` of both libraries with -warnaserror is clean; fast tests: Curl.Authentication.UnitTests 921 passed, Curl.Protocol.Http.UnitTests 2233 passed. Measure-CodeQuality for Curl.Authentication.UnitLibrary: 0 failing members. The tests are platform-neutral (in-memory connection, no paths).

## Log

- 2026-10-22: Created.
- 2026-10-22: Backlog -> Doing. Criteria at claim: 0a072548 b63de15c fc0592ec d60bebb2.
- 2026-10-22: Doing -> Done. -u : and -U : send no Authorization or Proxy-Authorization header and no auth using line, as curl 8.22.0
