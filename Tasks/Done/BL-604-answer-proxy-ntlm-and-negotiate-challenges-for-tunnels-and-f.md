---
id: BL-604
title: Answer proxy NTLM and Negotiate challenges for tunnels and forward proxies
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-602, BL-603, BL-526, BL-527]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Console, Curl.Console.UnitTests, Curl.Authentication.UnitLibrary, Curl.Authentication.UnitTests, Documentation/Planning/Decisions/ADR-0270-proxy-ntlm-and-negotiate-go-on-over-the-same-proxy-connection-as-the-origins-do.md, Documentation/Planning/Decisions/README.md]
requirement: none
created: 2026-09-28
completed: 2026-09-30
---
# BL-604 — Answer proxy NTLM and Negotiate challenges for tunnels and forward proxies

## Goal

`--proxy-ntlm` and `--proxy-negotiate` (and `--proxy-anyauth` when they rank first) complete their handshakes against a `407` on the same proxy connection, for `CONNECT` tunnels and forward proxy requests, using the NTLM and Negotiate implementations from BL-526 and BL-527, as curl 8.21.0 does, on every platform (standing rule, root `CLAUDE.md`, "Decisions", 2026-09-28).

## Context

- Conformance audit 2026-09-28, row 14 (Major). Builds on BL-602 (tunnel), BL-603 (forward) and BL-525's ADR (the token seam).
- NTLM authenticates the connection, so the tunnel's retry must stay on one connection.

## Acceptance criteria

- [x] Measured first with `Record-CurlExchange.ps1` (`-HoldOpenMilliseconds` so the retry reuses the connection) as the proxy: `--proxy-ntlm -U u:p` for a tunnel and for a forward request with a fixed Type 2 challenge; request bytes, stderr and exit code copied into Notes.
- [x] Tests pin the three-leg exchange for both paths with the seam's fixed inputs, and the Negotiate header with a fake token source.
- [x] `dotnet build Curl.slnx -warnaserror` is clean, the fast tests pass, and `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for each library changed.

## Notes

- Measured curl 8.21.0 (mingw, Schannel, SSPI) 2026-09-30 with `Record-CurlExchange.ps1 -Script` (one connection, steps `read`/`send`; `-Script` rather than `-HoldOpenMilliseconds`, which holds a connection only after its one response, as BL-526 found), the Type 2 being MS-NLMP 4.2.4.3's CHALLENGE `TlRMTVNTUAACAAAADAAMADgAAAAzgoriASNFZ4mrze8AAAAAAAAAACQAJABEAAAABgBwFwAAAA9TAGUAcgB2AGUAcgACAAwARABvAG0AYQBpAG4AAQAMAFMAZQByAHYAZQByAAAAAAAA` in `HTTP/1.1 407 Proxy Authentication Required` + `Proxy-Authenticate: NTLM <Type 2>` + `Content-Length: 0`:
  - Tunnel, `-s -S -v -p -x http://127.0.0.1:18604 --proxy-ntlm -U u:p http://example.test/`, replies 407 Type 2, `HTTP/1.1 200 Connection established`, then `200 OK` `ok` inside: `CONNECT example.test:80 HTTP/1.1` / `Host: example.test:80` / `Proxy-Authorization: NTLM TlRMTVNTUAABAAAAB4IIogAAAAAAAAAAAAAAAAAAAAAKAPRlAAAADw==` / `User-Agent: curl/8.21.0` / `Proxy-Connection: Keep-Alive`; then on the same connection the same CONNECT with `Proxy-Authorization: NTLM TlRMTVNTUAADAAAAGAAYAHgAAADcANwAk...` (SSPI's Type 3: flags 0xA2888205, a MIC, the workstation this machine's name, target name `HTTP/127.0.0.1`, i.e. the proxy's host); then `GET / HTTP/1.1` in the tunnel. stdout `ok`, stderr empty with `-sS`, exit 0. `-v`: `Proxy auth using NTLM with user 'u'` and `Establishing HTTP proxy tunnel to example.test:80` before each CONNECT.
  - Forward, `-s -S -v -x http://127.0.0.1:18605 --proxy-ntlm -U u:p http://example.test/`, replies 407 Type 2 then `200 OK` `ok`: `GET http://example.test/ HTTP/1.1` / `Host: example.test` / `Proxy-Authorization: NTLM <the same Type 1>` / `User-Agent: curl/8.21.0` / `Accept: */*` / `Proxy-Connection: Keep-Alive`; then (`Reusing existing http: connection with proxy 127.0.0.1`) the same request with the Type 3. stdout `ok`, exit 0.
  - Tunnel, 407 Type 2 then 407 bare `NTLM`: Type 1, Type 3, `-v` `NTLM handshake rejected`, `NTLM authentication problem, ignoring.`; stderr `curl: (7) CONNECT tunnel failed, response 407`, exit 7. Forward, the same: two requests, the second 407 is the result, stdout empty, exit 0.
  - `--proxy-negotiate -U :` against `407` + `Proxy-Authenticate: Negotiate` (no ticket): no `Proxy-Authorization`, `-v` `InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS (0x8009030e) - No credentials are available in the security package` then `Proxy auth using Negotiate with user ''` before the first request, and the failure line again after the 407. Forward: one request, stdout `deny` (the 407's body), exit 0. Tunnel: one CONNECT, stderr `curl: (7) InitializeSecurityContext failed: SEC_E_NO_CREDENTIALS ...` (the failure line, not `CONNECT tunnel failed`), exit 7 - filed as BL-1032.
  - Forward `--proxy-anyauth -U u:p`, 407 bare `NTLM`, 407 Type 2, 200: no header, Type 1, Type 3, all on one connection; `ok`, exit 0.
- Plan and decision: ADR-0270. `RankedHttpAuthenticator` drops its "origin only" gates, so a proxy request is answered on the origin's terms (`--proxy-ntlm` alone sends Type 1 up front; `--proxy-negotiate` alone steps a context before the first request and after a 407 without `-U`). The proxy's `HttpAuthRequest` carries the proxy's own URL, so the SPN is `HTTP/<proxy host>` as measured; Digest still hashes the request target. `HttpProtocolHandler` makes the forward proxy's first value with `CreateAuthorizationAsync`, reporting its lines before `Proxy auth using ...`; its 407 retry already went through `ContinueAuthorizationAsync` (BL-603). `TcpConnector` now continues a 407 to a CONNECT that sent a credential through `ContinueAuthorizationAsync` (tracking whether the value answered a challenge, across a redial too), turns `HttpAuthenticationFailedException` into the tunnel's failure (exit 94), and ends a kept Negotiate context when the tunnel opens or the reply is not a 407.
- Found and fixed: the tunnel's authenticator was built over `SystemSecurityContextFactory`, so off Windows proxy NTLM would have gone to the system GSS-API rather than curl's own NTLM. `CurlComposition.CreateTransports` now builds it over a `LateBoundSecurityContextFactory` bound to `CreateSecurityContextFactory`'s router once the connectors (which it needs for KDC exchanges, and which need the tunnel options) exist.
- Tests: the token source hands out the measured SSPI Type 1 and, for Type 3, curl 8.18.0's own-NTLM Type 3 for the same Type 2 (BL-526), since SSPI's Type 3 carries this machine's name; the request bytes around them are curl's. `Curl.Networking.UnitTests` links `Curl.Protocol.Http.UnitTests/Fakes/ScriptedTokenSource.cs` (now recording the context requests) rather than copying it.
- Sensible defaults taken (ADR-0270): a tunnel's `--proxy-anyauth` whose Negotiate context makes no token sends no second CONNECT (curl sends one without the header and fails on its 407 with the same exit 7); a Type 3 for a 407 that closed the connection goes out on a new connection.
- Touches added, per rule 3 (Doing held only BL-1003 `Record-CurlExchange.ps1` and BL-1031 `RunDarkFactory.ps1`): `Curl.Authentication.UnitLibrary`/`.UnitTests` (the "proxy is another task's" gates lived in `RankedHttpAuthenticator`), the ADR-0270 file and the decisions README.
- Follow-ups: BL-1032 (tunnel stderr for a failed proxy Negotiate context). The tunnel's `-v` handshake lines (`Proxy auth using NTLM with user 'u'`, `NTLM handshake rejected`) belong to BL-863, which covers the CONNECT retry's `-v` lines.
- Gates: `dotnet build Curl.slnx -warnaserror` clean; fast tests: Curl.Authentication.UnitTests 714, Curl.Networking.UnitTests 1680, Curl.Protocol.Http.UnitTests 1455, Curl.Console.UnitTests 1840 passed; the only failures in full-solution runs were two real-port bind tests in Curl.Networking.UnitTests (`DisposeAsync_ReleasesThePortSoItCanBeBoundAgain`, `BindLocalEnd_WhenTheFirstPortIsInUse_BindsTheNext`), each in a different run, both passing three times running the project alone - port contention with the other lanes. `Measure-CodeQuality.ps1`: Curl.Authentication.UnitLibrary, Curl.Networking.UnitLibrary, Curl.Protocol.Http.UnitLibrary and Curl.Console each 100/100, 0 failing, worst CRAP 10.

## Log

- 2026-09-28: Created.
- 2026-09-30: Backlog -> Doing.
- 2026-09-30: Doing -> Done. --proxy-ntlm and --proxy-negotiate answer a 407 on the same proxy connection for CONNECT tunnels and forward requests: Type 1 up front, Type 3 for the Type 2, Negotiate's next token, for HTTP on the proxy's host (ADR-0270)
