---
id: BL-986
title: Send the Digest and NTLM Authorization value beside an -H Authorization header, as curl does
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-986 — Send the Digest and NTLM Authorization value beside an -H Authorization header, as curl does

## Goal

`curl --digest -u u:p -H "Authorization: x"` (and `--ntlm`) sends curl's own `Authorization: Digest ...` line before the `-H` one on the request that answers the 401, as curl 8.21.0 does, instead of dropping it.

## Context

- Measured by BL-954 (its Notes) with `Record-CurlExchange.ps1` against curl 8.21.0 Schannel: `-v -H "Authorization: x" --digest -u u:p` against a 401 Digest challenge sends `Authorization: x` on the first request, then `Authorization: Digest username="u",...` followed by `Authorization: x` on the second. libcurl only checks `-H Authorization` for Basic and Bearer (`output_auth_headers` in lib/http.c).
- `HttpRequestHeadFormatter.Format` writes the authenticator's value with `AppendUnlessOverridden`, which drops it whenever an `-H` value names `Authorization`; Basic and Bearer are right to drop it, Digest and NTLM are not.
- Measure NTLM the same way before pinning it.

## Acceptance criteria

- [ ] A `Curl.Protocol.Http.UnitTests` test pins the measured request bytes of `--digest -u u:p -H "Authorization: x"` against a Digest 401, both requests.
- [ ] A test pins `-u u:p -H "Authorization: x"` still sends only `Authorization: x` (Basic dropped).
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Filed by BL-954.

## Log

- 2026-09-29: Created.
