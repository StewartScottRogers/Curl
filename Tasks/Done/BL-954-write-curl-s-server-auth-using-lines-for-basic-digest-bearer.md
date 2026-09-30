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
completed: 2026-09-29
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

- [x] A `Curl.Protocol.Http.UnitTests` test pins the measured `-v` events, in curl's order, for `-u u:p`, `--digest -u u:p` (both requests), `--oauth2-bearer tok`, `--ntlm -u u:p` and a Basic forward proxy.
- [x] A test pins that `-H "Authorization: x" -u u:p` writes no Basic line, if measured so.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

Measured 2026-09-29 with `Record-CurlExchange.ps1` against curl 8.21.0 (x86_64-w64-mingw32, Schannel), `-v`, loopback server (only the `* using`, `* ... auth using` and request lines shown):

- `-u u:p`: `using HTTP/1.x`, `Server auth using Basic with user 'u'`, request with `Authorization: Basic dTpw`.
- `-H "Authorization: x" -u u:p` and `-H "Authorization: x" --oauth2-bearer tok`: no line; only `Authorization: x` sent.
- `--oauth2-bearer tok`: `Server auth using Bearer with user ''`; with `-u u:p` as well: `... with user 'u'`.
- `--digest -u u:p` against a Digest 401: `Server auth using Digest with user 'u'` before both requests; the first sends no `Authorization`.
- `--digest` without `-u`: no line. `--anyauth -u u:p`: no line on the first request, the Digest line on the retry.
- `-H "Authorization: x" --digest -u u:p`: the Digest line is still written, and the retry sends curl's `Authorization: Digest ...` **and** `Authorization: x`. Curl here drops its own value; filed as BL-986.
- `--ntlm -u u:p`: `Server auth using NTLM with user 'u'` before every request.
- `-x http://proxy -U pu:pp`: `Proxy auth using Basic with user 'pu'`; with `-u u:p` too, the proxy line comes first, then the server line. `-H "Proxy-Authorization: x"` does not stop the proxy line.
- `--proxy-digest` / `--proxy-ntlm -U pu:pp`: `Proxy auth using Digest|NTLM with user 'pu'` before each request (Digest's first with no header).
- `-L -u u:p` (and `--digest`) redirected to another host: the line on the first request only.
- `--aws-sigv4 ... -u ak:sk`: the two `aws_sigv4:` lines, then `Server auth using AWS_SIGV4 with user 'ak'`.

Design: `HttpAuthUsingLines` decides the scheme from the value sent (its first token), from `HttpNegotiateInfoLines.PicksNegotiate` for Negotiate, and from `--digest` alone with a user for Digest's valueless first request; `HttpProtocolHandler.ReportAuthorizationLines` writes the proxy line, then the authenticator's own lines, then the server line, following libcurl's `Curl_http_output_auth` (proxy first, host second). `HttpNegotiateInfoLines.ServerAuthUsing` moved into it. The `AWS_SIGV4` line stays with `Curl.Console`'s `AwsSigV4HttpAuthenticator` (BL-629), which already writes it after its signing lines, so the HTTP handler writes none for a signed request. No ADR: every behaviour here is measured, not chosen.

## Log

- 2026-09-29: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. -v writes curl's Server auth using / Proxy auth using lines for Basic, Bearer, Digest, NTLM and Negotiate before each request
