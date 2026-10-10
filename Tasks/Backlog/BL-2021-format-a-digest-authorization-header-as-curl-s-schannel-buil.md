---
id: BL-2021
title: Format a Digest Authorization header as curl's Schannel build does, without a space after each comma
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-10
completed:
---
# BL-2021 — Format a Digest Authorization header as curl's Schannel build does, without a space after each comma

## Goal

`curl --digest -u u:p -v` against a Digest 401 sends `Authorization: Digest username="u",realm="r",nonce="abc",uri="/64",response="..."` (no space after each comma) on Windows, as curl 8.21.0's Schannel build (SSPI) does; the OpenSSL build keeps `, `.

## Context

Found by BL-2018 on 2026-10-10 with `Record-CurlExchange.ps1 -HalfCloseAfterResponse -HoldOpenMilliseconds 1500 -Connections 2 -Response '<401 Digest realm="r", nonce="abc", Content-Length: 26>','HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok' -CurlArgs '--digest','-u','u:p','-v','-s','http://127.0.0.1:{port}/64'`:

- curl 8.21.0 (Schannel): `Authorization: Digest username="u",realm="r",nonce="abc",uri="/64",response="fc3de222db74c3ec88aabb5510c76f80"` (request bytes 274).
- Curl (`Curl.Console` Debug build): `Authorization: Digest username="u", realm="r", nonce="abc", uri="/64", response="fc3de222db74c3ec88aabb5510c76f80"` (278).

The response hash matches; only the separators differ. `HttpProtocolHandlerTests.Authentication.cs`' `DigestValue` already pins curl's comma-only form through a scripted authenticator, so the gap is in the production Digest builder the console composes (find it from `HttpAuthSchemes.Digest` in `Curl.Console/CurlComposition.cs`; probably `Curl.Authentication.UnitLibrary`). Check the OpenSSL build's form on Linux before changing it there. Widen `touches` if the builder lives elsewhere.

## Acceptance criteria

- [ ] On Windows the Digest `Authorization` value Curl sends matches curl 8.21.0's bytes for the command above (measured, recorded under Notes).
- [ ] A unit test pins the Schannel-build form, and one pins the OpenSSL-build form as measured.
- [ ] Every changed `*.UnitLibrary` keeps 100% line and branch coverage; `dotnet build -warnaserror` is clean and the fast tests are green.
- [ ] No option changes, so `--ai-help` needs nothing; say so under Notes.

## Notes

## Log

- 2026-10-10: Created.
