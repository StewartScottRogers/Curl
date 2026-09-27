---
id: BL-238
title: Compose proxy selection with the tunnelling connector and forward proxying in Curl.Console
priority: Normal
assignee: Claude
pipeline: feature
depends-on: [BL-206, BL-212, BL-183, BL-192, BL-231]
touches: [Curl.Console, Curl.Console.UnitTests, Documentation/Planning/Decisions]
requirement: none
created: 2026-09-26
completed: 2026-09-27
---
# BL-238 — Compose proxy selection with the tunnelling connector and forward proxying in Curl.Console

## Goal

`-x`, `-U`, `--noproxy`, `-p` and the proxy environment variables choose a proxy with BL-206 and route through BL-212's connector or BL-183's forward proxying.

## Context

- Filed from the Phase 1 HTTP plan (protocol-architect, 2026-09-26), item W9. Upstream references: https://curl.se/docs/manpage.html and https://curl.se/libcurl/c/libcurl-errors.html; behaviour measured on curl 8.21.0.
- BL-231 added as a dependency beyond the plan.
- Where a criterion says *measured*, run curl 8.21.0 - the mingw build `/mingw64/bin/curl`, first on PATH, which is the Windows reference (ADR-0009 and the BL-153 ADR) - against a loopback server (the BL-165 recording script `Record-CurlExchange.ps1` once it exists), record the exact command and the bytes it produced in `Notes`, then pin them in a test. Never pin text that was not measured.

## Acceptance criteria

- [x] http via proxy, https via proxy and `-p` each send measured bytes over the fake connector; the 407 case exits 7 with the measured line.
- [x] `dotnet build Curl.Console -warnaserror` is clean; `dotnet test --filter "TestCategory!=Integration"` passes and no new test needs `TestCategory=Integration`; `Measure-CodeQuality.ps1` reports 100% line and branch coverage and no failing member for `Curl.Console`.

## Notes

- From BL-206 (ADR-0024): construct `new ProxySelector(Environment.GetEnvironmentVariable)` and call `TrySelect(url, -x text, --noproxy text, ...)`; its failure is the transfer's result. It takes no `-U`, `--proxy1.0` or `--socks*` input: `-U` replaces `ProxyEndpoint.Credential`, and the kind options are this task's to combine.
- Plan item: W9 in the Phase 1 HTTP plan (2026-09-26); plan keys in this file were replaced by their task IDs.
- Touches widened to `Documentation/Planning/Decisions` for ADR-0051 and its index row; no task in Doing (BL-030, BL-214) names it. ADR-0051 was the next free number in this checkout; another lane may take it too, so check for a clash when integrating.
- Delivered: `Curl.Console/TransferProxySelection.cs` calls `ProxySelector.TrySelect(url, -x/--socks text, the option's kind, --noproxy)`, lets `-U` replace the credential, and refuses tunnels the connector cannot open yet. `TransferDispatch` holds the `ProxySelector` (production: `Environment.GetEnvironmentVariable`; `CreateRunner(connector, ...)` takes one for tests). `CurlCommandRunner.TransferWithBodyAsync` selects after the URL, `-r` and `-F` checks and threads the proxy to `TransferContextFactory.Create` -> `HttpRequestOptionsMapping` (`ForwardProxy`, `ProxyTunnel`). Tests: `CurlCompositionProxyTests` (17 cases), the tunnel cases through a real `TcpConnector` over new fakes `ScriptedTcpDialer`, `LoopbackDnsResolver`, `PassThroughTlsProvider`.
- Decision (ADR-0051, Decided by Claude under Stewart's delegation): `TcpConnector` throws `NotSupportedException` for SOCKS and HTTPS-proxy tunnels (BL-213, BL-266 in Backlog), so an `http`/`https` transfer that needs one - any SOCKS proxy, an HTTPS proxy for `https`, under `-p` or under `-L` - ends with exit 4 `Unsupported proxy '<host>:<port>', Curl cannot tunnel through a <kind> proxy yet` instead of crashing. Curl's own text, not curl's. Removal filed as BL-328.
- Default taken: `--proxy1.0` is not parsed yet (not in this task's goal), so `Http10` never comes from the command line; the tunnel's `User-Agent` stays the connector default until BL-267.
- Follow-ups filed with the board script: BL-328 (remove the exit-4 guard once BL-213/BL-266 land), BL-329 (choose the proxy again per redirect hop; today hops keep the first URL's proxy), BL-330 (non-HTTP schemes ignore the proxy and connect directly; decide their routes in an ADR).
- Code review (code-reviewer): fixed the `-L` hole (an HTTPS proxy forwarded for an `http` URL could reach the connector as a tunnel on an `https` hop) and made `ScriptedTcpDialer` fail clearly on a failed scripted connect.
- Measured 2026-09-27, `/mingw64/bin/curl` 8.21.0 (Schannel), `Record-CurlExchange.ps1 -Port 18238` as the proxy unless noted:
  - `-sS -x 127.0.0.1:18238 http://example.com/a` -> `GET http://example.com/a HTTP/1.1
Host: example.com
User-Agent: curl/8.21.0
Accept: */*
Proxy-Connection: Keep-Alive

`, exit 0, body on stdout. Same bytes with `http_proxy=http://127.0.0.1:18238` and no `-x` (`/e` path).
  - `-sS -x http://127.0.0.1:18238 -U u:p http://example.com/a` -> `Proxy-Authorization: Basic dTpw` between `Host` and `User-Agent`.
  - `-sS -p -x 127.0.0.1:18238 http://example.com/a`, answered 407 -> `CONNECT example.com:80 HTTP/1.1
Host: example.com:80
User-Agent: curl/8.21.0
Proxy-Connection: Keep-Alive

`, exit 7, stderr `curl: (7) CONNECT tunnel failed, response 407`.
  - `-sS -x 127.0.0.1:18238 -U u:p https://example.com/a`, answered 407 -> `CONNECT example.com:443 HTTP/1.1`, `Host: example.com:443`, `Proxy-Authorization: Basic dTpw`, `User-Agent`, `Proxy-Connection: Keep-Alive`; exit 7 with the same line.
  - A Python loopback proxy on 18239 answering `200 Connection established` then `200 OK hello`: `-sS -p -x 127.0.0.1:18239 -U u:p http://example.com/a` -> CONNECT as above with `Proxy-Authorization: Basic dTpw`, then `GET /a HTTP/1.1
Host: example.com
User-Agent: curl/8.21.0
Accept: */*

` through the tunnel; exit 0, `hello`. The https-through-tunnel request is inside TLS and was not read; the test pins the same origin-form GET BL-231 measured for https.
  - `-sS -x 127.0.0.1:1 --noproxy 127.0.0.1 http://127.0.0.1:18238/a` -> direct `GET /a HTTP/1.1` with `Host: 127.0.0.1:18238`.
  - `--socks5 127.0.0.1:18238` sends `05 02 00 01` (curl supports it; Curl exits 4 until BL-328). `-x foo://...` exit 7 text is BL-206's measurement.
- Gates: `dotnet build -warnaserror` clean; `Measure-CodeQuality.ps1 -Library Curl.Console` ran the full fast suite, all passed (Curl.Console.UnitTests 425), Curl.Console 100% line, 100% branch, 180 members, 0 failing, worst CRAP 10. No new test is `Integration`.

## Log

- 2026-09-26: Created.
- 2026-09-27: Backlog -> Doing.
- 2026-09-27: Doing -> Done. curl -x, -U, --noproxy, -p and the proxy variables route http through BL-183's forward proxying and https/-p through BL-212's CONNECT tunnel, with curl 8.21.0's bytes and its exit-7 407 line
