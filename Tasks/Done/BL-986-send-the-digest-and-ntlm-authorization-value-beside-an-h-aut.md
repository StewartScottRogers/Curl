---
id: BL-986
title: Send the Digest and NTLM Authorization value beside an -H Authorization header, as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-29
completed: 2026-09-30
---
# BL-986 — Send the Digest and NTLM Authorization value beside an -H Authorization header, as curl does

## Goal

`curl --digest -u u:p -H "Authorization: x"` (and `--ntlm`) sends curl's own `Authorization: Digest ...` line before the `-H` one on the request that answers the 401, as curl 8.21.0 does, instead of dropping it.

## Context

- Measured by BL-954 (its Notes) with `Record-CurlExchange.ps1` against curl 8.21.0 Schannel: `-v -H "Authorization: x" --digest -u u:p` against a 401 Digest challenge sends `Authorization: x` on the first request, then `Authorization: Digest username="u",...` followed by `Authorization: x` on the second. libcurl only checks `-H Authorization` for Basic and Bearer (`output_auth_headers` in lib/http.c).
- `HttpRequestHeadFormatter.Format` writes the authenticator's value with `AppendUnlessOverridden`, which drops it whenever an `-H` value names `Authorization`; Basic and Bearer are right to drop it, Digest and NTLM are not.
- Measure NTLM the same way before pinning it.

## Acceptance criteria

- [x] A `Curl.Protocol.Http.UnitTests` test pins the measured request bytes of `--digest -u u:p -H "Authorization: x"` against a Digest 401, both requests.
- [x] A test pins `-u u:p -H "Authorization: x"` still sends only `Authorization: x` (Basic dropped).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Filed by BL-954.

- Measured 2026-09-30, curl 8.21.0 Schannel, `Record-CurlExchange.ps1`: `--digest -u u:p -H "Authorization: x"` against a Digest 401 sends `Authorization: x` alone, then `Authorization: Digest username="u",...` after `Host` and `Authorization: x` last. `--ntlm -u u:p -H "Authorization: x"` (served with `-Script`: read, 401 with a Type 2, read, 200, so both legs share one connection) sends `Authorization: NTLM <Type 1>` then `Authorization: NTLM <Type 3>`, each after `Host` and before `Authorization: x`. A two-connection `-Connections 2` NTLM run hangs the recorder; use `-Script`.
- `HttpRequestHeadFormatter.AppendAuthorization` sends a Digest, NTLM or Negotiate value always and any other value (Basic, Bearer, `--aws-sigv4`) only without an `-H Authorization`. Negotiate follows libcurl's source (`output_auth_headers` checks the custom headers for Basic and Bearer alone), not a measurement: recorded as ADR-0271.
- Added `Documentation/Planning/Decisions` to `touches` for ADR-0271 and its index row; no task in Doing names it.
- Tests: `HttpProtocolHandlerTests.AuthorizationBesideHeader.cs` (Digest, NTLM and Basic through the handler) and `Format_AuthorizationBesideAnAuthorizationHeader_SendsOnlyDigestNtlmAndNegotiate`. `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary`: 100% line, 100% branch, 0 failing members.

## Log

- 2026-09-29: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. curl --digest/--ntlm -u u:p -H 'Authorization: x' sends curl's own Digest or NTLM value beside the -H one, as curl 8.21.0 does
