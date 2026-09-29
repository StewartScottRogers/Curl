---
id: BL-603
title: Answer a forward proxy's 407 with the proxy auth scheme chosen
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-601]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0187-a-forward-proxy-s-407-is-answered-once-like-a-401.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-603 — Answer a forward proxy's 407 with the proxy auth scheme chosen

## Goal

A plain `http://` request through `-x` that the proxy answers `407` is retried with `Proxy-Authorization` for the scheme the proxy auth set and libcurl's ranking pick, alongside any server `Authorization`, as curl 8.21.0 does.

## Context

- Conformance audit 2026-09-28, row 14 (Major). Options: BL-601; NTLM and Negotiate: BL-604.
- Forward-proxy requests are written by `Curl.Protocol.Http.UnitLibrary` (FR-090); the 401 retry logic (ADR-0034) is the model for 407. The authenticator is injected through `IHttpAuthenticator`.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Connections 2` as the proxy: `-x http://127.0.0.1:<P> -U u:p http://example.invalid/` with `--proxy-basic`, `--proxy-digest`, `--proxy-anyauth`, and both `-U` and `-u` with a `407` then a `401`; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Protocol.Http.UnitTests` pin the requests and outcome for each case.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured curl 8.21.0 (mingw, Schannel) 2026-09-29 with `Record-CurlExchange.ps1 -Port 18603 -Connections <N>` as the proxy, `curl -s -S -v -x http://127.0.0.1:18603 -U u:p <switches> http://example.invalid/`. Replies: `P407b` = `HTTP/1.1 407 Proxy Authentication Required` + `Proxy-Authenticate: Basic realm="r"`; `P407d` = the same with `Proxy-Authenticate: Digest realm="r", nonce="abc", qop="auth"`; `S401b` / `S401d` = `HTTP/1.1 401 Unauthorized` + `WWW-Authenticate: Basic realm="s"` / `Digest realm="s", nonce="xyz", qop="auth"`; each with `Content-Length: 3` (body `PPP` or `UUU`) and `Connection: close`; `200` = `Content-Length: 2`, body `ok`. Every request is `GET http://example.invalid/ HTTP/1.1`, `Host: example.invalid`, [`Proxy-Authorization`], [`Authorization`], `User-Agent: curl/8.21.0`, `Accept: */*`, `Proxy-Connection: Keep-Alive`. Only the auth headers are listed:
  - `--proxy-basic`, P407b: 1 request, `Proxy-Authorization: Basic dTpw`. stdout `PPP`, exit 0; `-v`: `Proxy auth using Basic with user 'u'`, `Basic authentication problem, ignoring.`
  - `-f --proxy-basic`, P407b: the same request; exit 22, stderr `curl: (22) The requested URL returned error: 407`.
  - `--proxy-digest`, P407d then 200: request 1 none; request 2 (new connection) `Proxy-Authorization: Digest username="u",realm="r",nonce="abc",uri="/",cnonce="8c2728ea340ca45e0fc1411a44d914b4",nc=00000001,response="7c30003a2d4e99796b67ba30c0234996",qop="auth"`; stdout `ok`, exit 0. `-v`: `Issue another request to this URL: 'http://example.invalid/'`.
  - `--proxy-digest`, P407d twice: request 2 Digest (cnonce `95f7476d91514799667a1c895ce15714`, response `039df8ae965737f3ebb7361b5ac12705`); no third; stdout `PPP`, exit 0. Under `-f`: exit 22, `curl: (22) The requested URL returned error: 407`, `-v` `Digest authentication problem, ignoring.`
  - `--proxy-digest`, P407b: 1 request, none; stdout `PPP`, exit 0.
  - `--proxy-anyauth`, P407b then 200: none, then `Basic dTpw`; `ok`, exit 0. P407b twice: none, `Basic dTpw`, stdout `PPP`, exit 0. P407d then 200: none, then Digest (cnonce `ac9d38277a649bc7d1b93233b9bcf15c`, response `9a1961b82b2ff80d0f088b26d553c8d5`); `ok`, exit 0.
  - `-u a:b` (Basic both), P407b: 1 request, `Proxy-Authorization: Basic dTpw` then `Authorization: Basic YTpi`; stdout `PPP`, exit 0.
  - `--proxy-anyauth -u a:b`, P407b, S401b: request 1 `Authorization: Basic YTpi`; request 2 both Basic; stdout `UUU`, exit 0.
  - `--proxy-digest -u a:b --digest`, P407d, S401d, 200: request 1 none; request 2 proxy Digest (cnonce `063231b54c58aa830f9917b0665bdaf8`, nc 1, response `8146a82aefc2f845325c0b151b67e80d`); request 3 proxy Digest same cnonce, **nc=00000002**, response `924da41f0f75d705a8c76efb5ad7d596`, then `Authorization: Digest username="a",realm="s",nonce="xyz",uri="/",cnonce="582287d88c3c0940fe2e132942e332bc",nc=00000001,response="1f89777280a8403ca258416cad303d5b",qop="auth"`; `ok`, exit 0.
  - `--proxy-anyauth -u a:b --digest`, S401d, P407d, 200: request 1 none; request 2 origin Digest (cnonce `f7604464c2453c62e1f5077435011686`, response `9355f1a62b7602de98380cd8232120b8`); request 3 proxy Digest (cnonce `c06ed45dc85f0a3e7b4671f765ef1672`, response `b54dfbe77d0102f798b83b924efa82c5`) and origin Digest **nc=00000002**, response `19b392bfec0f8a5d87a9d959622271e4`; `ok`, exit 0.
  - `--proxy-digest`, P407d without `Connection: close` (`-HoldOpenMilliseconds`): the Digest answer went out on the same connection.
- Plan and decision: ADR-0187. `HttpProtocolHandler` takes `proxyAuthSchemes` (default Basic) through its constructor; `CurlComposition.CreateProtocolHandlers` passes `ProxyTunnelOptions.ProxyAuthSchemes` (production) or `options.ProxyAuthSchemes` (test runner). `HttpAuthRequest` for the proxy uses the origin-form target, which is the `uri` curl hashes. A 407 is retried on ADR-0034's terms, independently of the 401, through a shared `AnswerChallengesAsync`. No change to `Curl.Protocol.Abstractions` (held by BL-751).
- The Digest hashes in `HttpProtocolHandlerTests.ProxyAuthentication.cs` come from the real `DigestAuthenticator` fed curl's measured cnonces and equal curl's, in curl's own-code format (ADR-0025). `Curl.Protocol.Http.UnitTests.csproj` now references `Curl.Authentication.UnitLibrary` for that (a test reference; Authentication itself is unchanged).
- Left out, filed as BL-867: the kept Digest answer's `nc=00000002`. Not reported, as for the origin today: the `-v` lines `Proxy auth using ...` and `... authentication problem, ignoring.`
- Touches added: the ADR-0187 file (no Doing task names it). `Curl.Console/CLAUDE.md` updated (inside `Curl.Console`).
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Protocol.Http.UnitTests 1295, Curl.Console.UnitTests 1521 passed, no failure in the solution); `Measure-CodeQuality.ps1` Curl.Protocol.Http.UnitLibrary 100/100, 0 failing (worst CRAP 10), Curl.Console 100/100, 0 failing.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A forward proxy's 407 is answered once with Proxy-Authorization for the --proxy-basic/--proxy-digest/--proxy-anyauth pick, alongside any origin Authorization, on the same or a new connection
