---
id: BL-2022
title: Format a Digest Authorization header as curl's Schannel build does, without a space after each comma
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-2022 — Format a Digest Authorization header as curl's Schannel build does, without a space after each comma

## Goal

`curl --digest -u u:p -v` against a Digest 401 sends `Authorization: Digest username="u",realm="r",nonce="abc",uri="/64",response="..."` (no space after each comma) on Windows, as curl 8.21.0's Schannel build (SSPI) does; the OpenSSL build keeps `, `.

## Context

Found by BL-2018 on 2026-10-10 with `Record-CurlExchange.ps1 -HalfCloseAfterResponse -HoldOpenMilliseconds 1500 -Connections 2 -Response '<401 Digest realm="r", nonce="abc", Content-Length: 26>','HTTP/1.1 200 OK\r\nContent-Length: 2\r\n\r\nok' -CurlArgs '--digest','-u','u:p','-v','-s','http://127.0.0.1:{port}/64'`:

- curl 8.21.0 (Schannel): `Authorization: Digest username="u",realm="r",nonce="abc",uri="/64",response="fc3de222db74c3ec88aabb5510c76f80"` (request bytes 274).
- Curl (`Curl.Console` Debug build): `Authorization: Digest username="u", realm="r", nonce="abc", uri="/64", response="fc3de222db74c3ec88aabb5510c76f80"` (278).

The response hash matches; only the separators differ. `HttpProtocolHandlerTests.Authentication.cs`' `DigestValue` already pins curl's comma-only form through a scripted authenticator, so the gap is in the production Digest builder the console composes (find it from `HttpAuthSchemes.Digest` in `Curl.Console/CurlComposition.cs`; probably `Curl.Authentication.UnitLibrary`). Check the OpenSSL build's form on Linux before changing it there. Widen `touches` if the builder lives elsewhere.

## Acceptance criteria

- [x] On Windows the Digest `Authorization` value Curl's `DigestAuthenticator` writes with `matchesSspiBuild: true` matches curl 8.21.0's bytes for the command above (measured, recorded under Notes). Wiring it into `Curl.Console` on Windows is split off to BL-2033 (Curl.Console is held by BL-1975).
- [x] A unit test pins the Schannel-build form (`CreateAuthorization_SspiBuild_MatchesCurlsSchannelBuild`, four measured rows), and one pins the OpenSSL-build form (`CreateAuthorization_OwnDigestCode_MatchesCurlsOpenSslBuild`).
- [x] Every changed `*.UnitLibrary` keeps 100% line and branch coverage; `dotnet build -warnaserror` is clean and the fast tests are green.
- [x] No option changes, so `--ai-help` needs nothing; say so under Notes.

## Notes

- Measured 2026-10-10 on Windows, curl 8.21.0 (x86_64-w64-mingw32, Schannel), `Record-CurlExchange.ps1 -OutDirectory <dir> -Port <p> -HalfCloseAfterResponse -HoldOpenMilliseconds 1500 -Connections 2 -Response <401>,<200> -CurlArgs '--digest','-u','u:p','-s','http://127.0.0.1:<p>/64'`:
  - `Digest realm="r", nonce="abc"` -> `Digest username="u",realm="r",nonce="abc",uri="/64",response="fc3de222db74c3ec88aabb5510c76f80"`
  - `+ opaque="op", algorithm=MD5` -> `Digest username="u",realm="r",nonce="abc",uri="/64",algorithm=MD5,response="fc3de222db74c3ec88aabb5510c76f80",opaque="op"`
  - `qop="auth"` -> `Digest username="u",realm="r",nonce="abc",uri="/64",cnonce="1789520a8086d8c6b386c5286eace9d3",nc=00000001,response="7929e56cf7fe409f959989397c308dd3",qop="auth"`
  - `qop="auth", opaque="op", algorithm=MD5` -> `Digest username="u",realm="r",nonce="abc",uri="/64",cnonce="896707c735f53ea0b5a4c92546a776b6",nc=00000001,algorithm=MD5,response="6619c920c1ac2d2ec458c3ba51e39426",qop="auth",opaque="op"`
- So SSPI differs in more than the separators: `algorithm` before `response`, `qop` quoted after it, `opaque` last. Implemented as `DigestAuthenticator`'s `matchesSspiBuild` parameter (default `false`), decided in ADR-0472.
- OpenSSL form: no Linux machine here; pinned from curl's `vauth/digest.c` layout, which the existing `CreateAuthorization_MeasuredChallenge_MatchesCurl` rows already pin (ADR-0025). Unchanged.
- `Curl.Console/CurlComposition.cs` must pass `matchesSspiBuild: OperatingSystem.IsWindows()`; Curl.Console is in BL-1975's `touches`, so filed as BL-2033 (depends on this task) rather than widening this task and requeueing it.
- Coverage (`Measure-CodeQuality.ps1 -Library Curl.Authentication.UnitLibrary`): 100% line, 100% branch. Its one failing member, `NetrcTokenScanner.Peek()` at complexity 12, predates this task (BL-1986); filed as BL-2034.
- No option changes; `--ai-help` needs nothing.

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. DigestAuthenticator writes curl's Schannel-build (SSPI) Digest layout with matchesSspiBuild; Console wiring is BL-2033
