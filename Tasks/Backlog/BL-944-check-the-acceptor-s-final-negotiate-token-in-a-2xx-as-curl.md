---
id: BL-944
title: Check the acceptor's final Negotiate token in a 2xx as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-842]
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Protocol.Abstractions.UnitLibrary, Curl.Protocol.Abstractions.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-944 — Check the acceptor's final Negotiate token in a 2xx as curl does

## Goal

A `2xx` answering a Negotiate request that carries `WWW-Authenticate: Negotiate <token>` (Kerberos mutual authentication) has that token stepped into the context that sent the request, and the transfer does what curl 8.21.0 does when the context accepts or rejects it.

## Context

- ADR-0226 (BL-842) keeps a Negotiate context awaiting its next leg in `NegotiateHttpAuthenticator`, keyed by the `Authorization` value it made, and continues it only on a 401. A context whose token drew a 200 is never stepped and stays in the dictionary.
- curl's `Curl_input_negotiate` runs for any response with a `WWW-Authenticate: Negotiate` header after a token was sent (lib/http.c `Curl_http_input_auth`, lib/http_negotiate.c); read curl 8.21.0's source to pin what a rejected final token does (exit code, message) before writing tests, and measure it where a KDC can be reached.
- The HTTP handler asks the authenticator nothing about a 2xx today, so the contract (`IHttpAuthenticator`) gains a call for it; record that in an ADR.

## Acceptance criteria

- [ ] A `Curl.Authentication.UnitTests` test steps a kept context with the token of a 200's `Negotiate` challenge and disposes of it.
- [ ] A `Curl.Protocol.Http.UnitTests` test pins the transfer's result for a final token the context accepts and one it rejects.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
