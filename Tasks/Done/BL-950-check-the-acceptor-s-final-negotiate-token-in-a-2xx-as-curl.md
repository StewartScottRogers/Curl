---
id: BL-950
title: Check the acceptor's final Negotiate token in a 2xx as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-842]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-29
---
# BL-950 — Check the acceptor's final Negotiate token in a 2xx as curl does

## Goal

A `2xx` answering a Negotiate request that carries `WWW-Authenticate: Negotiate <token>` (Kerberos mutual authentication) has that token stepped into the context that sent the request, and the transfer does what curl 8.21.0 does when the context accepts or rejects it.

## Context

- ADR-0227 (BL-842) keeps a Negotiate context awaiting its next leg in `NegotiateHttpAuthenticator`, keyed by the `Authorization` value it made, and continues it only on a 401. A context whose token drew a 200 is never stepped and stays in the dictionary.
- curl's `Curl_input_negotiate` runs for any response with a `WWW-Authenticate: Negotiate` header after a token was sent (lib/http.c `Curl_http_input_auth`, lib/http_negotiate.c); read curl 8.21.0's source to pin what a rejected final token does (exit code, message) before writing tests, and measure it where a KDC can be reached.
- The HTTP handler asks the authenticator nothing about a 2xx today, so the contract (`IHttpAuthenticator`) gains a call for it; record that in an ADR.

## Acceptance criteria

- [x] A `Curl.Authentication.UnitTests` test ~~steps~~ ends a kept context on a 200's `Negotiate` challenge and disposes of it - unstepped, as curl never steps it (see Notes): `NegotiateHttpAuthenticatorTests.EndAuthorization_A200CarriedTheAcceptorsFinalToken_DisposesTheKeptContextWithoutSteppingIt`, `RankedHttpAuthenticatorTests.EndAuthorization_NegotiateContextKeptForTheNextLeg_DisposesOfItWithoutSteppingIt`.
- [x] A `Curl.Protocol.Http.UnitTests` test pins the transfer's result for a final token the context accepts and one it rejects: `ExecuteAsync_200CarriesTheAcceptorsFinalNegotiateToken_TakesThe200AndDisposesTheContextUnstepped` (both: the 200's body, exit 0, one request, no failure line).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- **curl does not check the token (ADR-0248).** curl 8.21.0's lib/http.c passes
  `WWW-Authenticate` to `Curl_http_input_auth` only when the status is 401 (line 3635) and
  `Proxy-authenticate` only on a 407 (line 3454); on a 2xx the Negotiate state just moves
  `GSS_AUTHDONE` -> `GSS_AUTHSUCC` (line 4001) and the context is never stepped. Read from the
  curl-8_21_0 tag's lib/http.c and lib/http_negotiate.c. So an accepted and a rejected final
  token both end as the 2xx, exit 0, with no `-v` failure line. No KDC on a lane machine, so
  pinned from the source with scripted tokens. ADR-0227's claim that curl checks it is
  corrected there.
- **Contract:** `IHttpAuthenticator.EndAuthorization(string sentAuthorization)`, default
  no-op. `HttpProtocolHandler.RetryOfAsync` calls it for the sent `Authorization` on a non-401
  and the sent `Proxy-Authorization` on a non-407; `RankedHttpAuthenticator` hands it to
  `NegotiateHttpAuthenticator.EndAuthorization`, which disposes of the kept context unstepped.
- The first criterion said "steps"; changed to "ends ... unstepped" because stepping would
  diverge from curl (a failure line curl never writes), which the Goal forbids.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0248, the ADR index row and the
  ADR-0227 correction; no task in Doing touches it.
- Follow-up BL-981: `Curl.Console`'s `AwsSigV4HttpAuthenticator` (always wrapping the ranked
  authenticator in the composition) does not forward `EndAuthorization` yet, and the WebSocket
  handler does not call it; `Curl.Console` is in BL-576's touches and `Curl.Protocol.Ws` in
  BL-955's, so they were not widened in here. Nothing observable depends on it.
- Test counts: Abstractions 629, Authentication 704 (+4 skipped), Http 1426 (+2 skipped); all
  fast tests green. Coverage 100/100, 0 failing members for all three libraries.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A 2xx's Negotiate token is left unstepped as curl 8.21.0 does (exit 0, the 2xx's body), and the kept context is disposed of via IHttpAuthenticator.EndAuthorization (ADR-0248)
