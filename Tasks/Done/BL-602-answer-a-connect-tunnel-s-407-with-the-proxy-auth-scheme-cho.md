---
id: BL-602
title: Answer a CONNECT tunnel's 407 with the proxy auth scheme chosen
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-601]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions/ADR-0186-a-connect-tunnel-answers-a-407-through-the-injected-proxy-authenticator.md]
requirement: none
created: 2026-09-28
completed: 2026-09-29
---
# BL-602 — Answer a CONNECT tunnel's 407 with the proxy auth scheme chosen

## Goal

A `CONNECT` answered `407` with `Proxy-Authenticate` is retried with `Proxy-Authorization` for the scheme the proxy auth set allows and libcurl's ranking picks (Basic sent up front; Digest and anyauth after the challenge), as curl 8.21.0 does, and a second `407` fails with curl's exit code and message.

## Context

- Conformance audit 2026-09-28, row 14 (Major). Options: BL-601. NTLM and Negotiate for proxies are BL-604.
- Code: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs`, `HttpProxyTunnelOptions.cs`, `HttpProxyTunnelReply.cs`; ranking and Digest in `Curl.Authentication.UnitLibrary` (ADR-0028, ADR-0025), composed in `Curl.Console` (ADR-0059: the tunnel takes credentials and encoding from the composition).
- FR-081 pins the current `407` failure.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1 -Connections 2` as the proxy: `-p -x http://127.0.0.1:<P> -U u:p` with `--proxy-basic`, `--proxy-digest` (a `407` with a fixed Digest challenge then `200 Connection established`), `--proxy-anyauth` against Basic and against Digest, and a second `407`; request bytes, stderr and exit code copied into Notes.
- [x] `Curl.Networking.UnitTests` pin the `CONNECT` requests and outcome for each case (Digest with an injected client nonce).
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured curl 8.21.0 (mingw, Schannel) 2026-09-29 with `Record-CurlExchange.ps1 -Port 18602 -Connections 3` as the proxy, `-s -S -p -x http://127.0.0.1:18602 -U u:p <switch> http://example.test/`, each `407` being `HTTP/1.1 407 Proxy Authentication Required` + `Proxy-Authenticate: Basic realm="r"` or `Digest realm="r", nonce="abc", qop="auth"` + `Content-Length: 0` + `Connection: close`, the success `HTTP/1.1 200 Connection established` (the exit 56 after each opened tunnel is the recorder closing before the GET inside it is answered):
  - `--proxy-basic`, 407 Basic: one CONNECT with `Proxy-Authorization: Basic dTpw` (before `User-Agent`); stderr `curl: (7) CONNECT tunnel failed, response 407`; exit 7.
  - `--proxy-digest`, 407 Digest then 200: CONNECT with no `Proxy-Authorization`; `-v` shows `Connect me again please` and a new connection carrying `Proxy-Authorization: Digest username="u",realm="r",nonce="abc",uri="example.test:80",cnonce="7f052e869469cb30acc85773266dfe11",nc=00000001,response="53a4df5585ad72bb9377dd4c05f58fc1",qop="auth"` (WDigest's format, ADR-0025); tunnel opens; exit 56 from the recorder.
  - `--proxy-anyauth` vs Basic: nothing first, then `Basic dTpw` on the new connection; tunnel opens. vs Digest: nothing first, then the Digest answer; tunnel opens.
  - `--proxy-digest`, 407 twice: the Digest answer, then `-v` `Digest authentication problem, ignoring.`, stderr `curl: (7) CONNECT tunnel failed, response 407`, exit 7. The same with `--proxy-anyauth` vs Basic twice (`Basic authentication problem, ignoring.`).
  - Without `Connection: close` curl sent the answer on the same connection (a single connection recorded).
- The Digest hash was checked: MD5 over `u:r:p`, `CONNECT:example.test:80` and the measured cnonce gives curl's `53a4df55...`, so `TcpConnectorTests.ProxyAuth` injects that cnonce and pins the same response in curl's own-code format (ADR-0025).
- Plan and decision: ADR-0186. `HttpProxyTunnelOptions` gains `ProxyAuthSchemes` and `ProxyAuthenticator`; `TcpConnector` asks it before the first CONNECT and once after a `407` to a CONNECT that sent nothing, reusing the connection (body discarded) unless the reply closes it or is chunked, else dialling again (TLS again for an HTTPS proxy). `Curl.Console` injects `CreateHttpAuthenticator`'s `RankedHttpAuthenticator` and `CommandLineOptions.ProxyAuthSchemes`. With no authenticator the tunnel keeps pre-emptive Basic (`PreemptiveBasicProxyAuthenticator`).
- Touches added: `Documentation/Planning/Decisions/ADR-0186-...md` for the ADR (no Doing task names it). `Curl.Networking.UnitTests.csproj` now references `Curl.Authentication.UnitLibrary` so the tests drive the real ranked authenticator and Digest; that is a test-project reference inside this task's touches and changes nothing in Authentication, which BL-538 holds.
- ADR numbers 0184 and 0185 are taken by other lanes' work, hence 0186.
- Left out, filed as follow-ups BL-855, BL-856 and BL-857: a chunked `407` body costs a new connection where curl reuses it; the retry's `-v` lines (`Connect me again please`, `Proxy auth using Digest with user 'u'`, `... authentication problem, ignoring.`) are not reported; a Digest `stale=true` retry is not made. FR-081 in Requirements.md still holds for Basic and was not edited (outside touches).
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests green (Curl.Networking.UnitTests 1361 passed, Curl.Console.UnitTests 1520 passed, no failure in the solution); `Measure-CodeQuality.ps1` Curl.Networking.UnitLibrary 100/100, 0 failing (worst CRAP 10), Curl.Console 100/100, 0 failing.

## Log

- 2026-09-28: Created.
- 2026-09-29: Backlog -> Doing.
- 2026-09-29: Doing -> Done. A CONNECT answered 407 is retried with Proxy-Authorization for the --proxy-basic/--proxy-digest/--proxy-anyauth pick, on the same or a new connection, and a second 407 fails with exit 7
