---
id: BL-954
title: Write curl's Server auth using lines for Basic, Digest, Bearer and NTLM, and Proxy auth using
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-843]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-09-29
completed:
---
# BL-954 — Write curl's Server auth using lines for Basic, Digest, Bearer and NTLM, and Proxy auth using

## Goal

Under `-v`, every HTTP request curl 8.21.0 sends with a scheme picked writes `* Server auth using <Scheme> with user '<user>'` (and `* Proxy auth using <Scheme> with user '<user>'` for a forward proxy) just before the request, as BL-843 does for Negotiate.

## Context

- ADR-0231 records the measured lines (BL-843 Notes): `-u u:p` writes `Server auth using Basic with user 'u'`; `--digest -u u:p` writes `Server auth using Digest with user 'u'` before both requests, the first of which sends no `Authorization`; `--oauth2-bearer tok` writes `Server auth using Bearer with user ''`.
- libcurl's `output_auth_headers` (lib/http.c) writes the line whenever the picked scheme equals the one wanted; Basic and Bearer only when no `-H Authorization` is given, and nothing once a redirect goes to another host without `--location-trusted`. Measure each of these with `Record-CurlExchange.ps1` before pinning, NTLM and the proxy line included.
- BL-843 put the Negotiate line in `HttpNegotiateInfoLines` and wrote it from `HttpProtocolHandler.ReportAuthorizationLines`; generalise it there rather than adding a second path.
- `Curl.Console.UnitTests` has no HTTP `-v` test with `-u` today (checked 2026-09-29); if one appears, add it to `touches`.

## Acceptance criteria

- [ ] A `Curl.Protocol.Http.UnitTests` test pins the measured `-v` events, in curl's order, for `-u u:p`, `--digest -u u:p` (both requests), `--oauth2-bearer tok`, `--ntlm -u u:p` and a Basic forward proxy.
- [ ] A test pins that `-H "Authorization: x" -u u:p` writes no Basic line, if measured so.
- [ ] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

## Log

- 2026-09-29: Created.
