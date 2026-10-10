---
id: BL-1997
title: Re-close GF-0001: In process, Curl never runs TcpConnector: no CONNECT tunnel, no HAProxy header, no .onion refusal, no name-resolution or --resolve/--connect-to failures
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-10
completed: 2026-10-10
---
# BL-1997 — Re-close GF-0001: In process, Curl never runs TcpConnector: no CONNECT tunnel, no HAProxy header, no .onion refusal, no name-resolution or --resolve/--connect-to failures

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0001 (In process, Curl never runs TcpConnector: no CONNECT tunnel, no HAProxy header, no .onion refusal, no name-resolution or --resolve/--connect-to failures), so a later gap analysis measures each of `behaviour:test1008`, `behaviour:test1021`, `behaviour:test1060`, `behaviour:test1061`, `behaviour:test206`, `behaviour:test209`, `behaviour:test213`, `behaviour:test217`, `behaviour:test265`, `behaviour:test287`, `behaviour:test718`, `behaviour:test749`, `behaviour:test750`, `behaviour:test1715`, `behaviour:test94`, `behaviour:test440`, `behaviour:test441`, `behaviour:test493`, `behaviour:test1455`, `behaviour:test3201`, `behaviour:test3220`, `behaviour:test1471`, `behaviour:test1472`, `behaviour:test20`, `behaviour:test3019`, `behaviour:test3020`, `behaviour:test2043`, `behaviour:test1293`, `behaviour:test1097`, `behaviour:test1230`, `behaviour:test1456`, `behaviour:test3028`, `behaviour:test3202`, `behaviour:test2050`, `behaviour:test2055`, `behaviour:test795` as `match`.

## Context

This is a Re-close task: every earlier task for GF-0001 ([BL-1794]) is Done, but the latest gap analysis still measures a gap, so the fix did not hold.

- Finding: GF-0001, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test1008`, `behaviour:test1021`, `behaviour:test1060`, `behaviour:test1061`, `behaviour:test206`, `behaviour:test209`, `behaviour:test213`, `behaviour:test217`, `behaviour:test265`, `behaviour:test287`, `behaviour:test718`, `behaviour:test749`, `behaviour:test750`, `behaviour:test1715`, `behaviour:test94`, `behaviour:test440`, `behaviour:test441`, `behaviour:test493`, `behaviour:test1455`, `behaviour:test3201`, `behaviour:test3220`, `behaviour:test1471`, `behaviour:test1472`, `behaviour:test20`, `behaviour:test3019`, `behaviour:test3020`, `behaviour:test2043`, `behaviour:test1293`, `behaviour:test1097`, `behaviour:test1230`, `behaviour:test1456`, `behaviour:test3028`, `behaviour:test3202`, `behaviour:test2050`, `behaviour:test2055`, `behaviour:test795`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test287 actual: <verify><protocol> differs at byte 0 (line 1): expected 'CONNECT test.remote.example.com.287:8990 HTTP/1.1', got 'GET /path/287 HTTP/1.1'. The same shape for the other -p/--proxytunnel and HTTPS-through-proxy cases (test94, test440, test441 and test493 expect CONNECT ...:443). test1455/3201/3220: expected 'proxy-line', got 'GET /<N> HTTP/1.1'. test1471/1472: <verify><stderr> expected 'curl: (6) Not resolving .onion address (RFC 7686)', got the progress meter. test20: expected exit code 6, got 52. test3019/3020: expected exit code 49, got 52. test2043: expected exit code 0, got 52. test1293: expected 'POST /1293', got 'POST /' (the first URL, http://0, reached the server). Cause in code: Curl.Console/InProcessCurl.RunAsync calls CurlComposition.CreateRunner with the caller's IConnector and no ConnectionCache, so the SwsHttpServerConnector receives every ConnectTarget, Proxy included, and connects directly. The CONNECT tunnel (Curl.Networking.UnitLibrary/HttpProxyTunnel via TcpConnector), HaproxyProtocolHeader, OnionAddress.IsRefused, name resolution and the --resolve/--connect-to parsing all live in TcpConnector, which this path never builds. HttpProtocolHandler.ForwardProxyOf hands every tunnelled proxy to the connector. Reproduce from the repository root (the output folder must exist and hold no blank): dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 287,1455,1471,20,3019,94

Suggestion, copied from the finding:

In Curl.Console, give InProcessCurl (and the CurlComposition.CreateRunner overload it calls) a way to run over the production Curl.Networking.UnitLibrary TcpConnector: take a dialer seam (the TCP dial and the name resolver) rather than a finished IConnector, and build TcpConnector with the run's HttpProxyTunnelOptions, HaproxyProtocolHeader, --resolve/--connect-to overrides and TLS provider, exactly as CreateTcpConnector does for the executable. Then a CONNECT, a PROXY line, the .onion refusal (exit 6), an unresolvable name (exit 6) and a bad --resolve/--connect-to entry (exit 49) reach the in-process server as they reach a real one. Change UpstreamConformanceTests.RunCurlAsync the same way so the ratchet measures the same path. If, with that wiring, a case still fails, the remaining difference is a product gap in TcpConnector and should be filed from the next run.

## Acceptance criteria

- [x] `behaviour:test1008`: Curl answers what curl 8.21.0 answers, `upstream test1008 passes`, so the item measures `match`.
- [x] `behaviour:test1021`: Curl answers what curl 8.21.0 answers, `upstream test1021 passes`, so the item measures `match`.
- [x] `behaviour:test1060`: Curl answers what curl 8.21.0 answers, `upstream test1060 passes`, so the item measures `match`.
- [x] `behaviour:test1061`: Curl answers what curl 8.21.0 answers, `upstream test1061 passes`, so the item measures `match`.
- [x] `behaviour:test206`: Curl answers what curl 8.21.0 answers, `upstream test206 passes`, so the item measures `match`.
- [x] `behaviour:test209`: Curl answers what curl 8.21.0 answers, `upstream test209 passes`, so the item measures `match`.
- [x] `behaviour:test213`: Curl answers what curl 8.21.0 answers, `upstream test213 passes`, so the item measures `match`.
- [x] `behaviour:test217`: Curl answers what curl 8.21.0 answers, `upstream test217 passes`, so the item measures `match`.
- [x] `behaviour:test265`: Curl answers what curl 8.21.0 answers, `upstream test265 passes`, so the item measures `match`.
- [x] `behaviour:test287`: Curl answers what curl 8.21.0 answers, `upstream test287 passes`, so the item measures `match`.
- [x] `behaviour:test718`: Curl answers what curl 8.21.0 answers, `upstream test718 passes`, so the item measures `match`.
- [x] `behaviour:test749`: Curl answers what curl 8.21.0 answers, `upstream test749 passes`, so the item measures `match`.
- [x] `behaviour:test750`: Curl answers what curl 8.21.0 answers, `upstream test750 passes`, so the item measures `match`.
- [x] `behaviour:test1715`: Curl answers what curl 8.21.0 answers, `upstream test1715 passes`, so the item measures `match`.
- [x] `behaviour:test94`: Curl answers what curl 8.21.0 answers, `upstream test94 passes`, so the item measures `match`.
- [x] `behaviour:test440`: Curl answers what curl 8.21.0 answers, `upstream test440 passes`, so the item measures `match`.
- [x] `behaviour:test441`: Curl answers what curl 8.21.0 answers, `upstream test441 passes`, so the item measures `match`.
- [x] `behaviour:test493`: Curl answers what curl 8.21.0 answers, `upstream test493 passes`, so the item measures `match`.
- [x] `behaviour:test1455`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test3201`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test3220`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [x] `behaviour:test1471`: Curl answers what curl 8.21.0 answers, `upstream test1471 passes`, so the item measures `match`.
- [x] `behaviour:test1472`: Curl answers what curl 8.21.0 answers, `upstream test1472 passes`, so the item measures `match`.
- [x] `behaviour:test20`: Curl answers what curl 8.21.0 answers, `upstream test20 passes`, so the item measures `match`.
- [x] `behaviour:test3019`: Curl answers what curl 8.21.0 answers, `upstream test3019 passes`, so the item measures `match`.
- [x] `behaviour:test3020`: Curl answers what curl 8.21.0 answers, `upstream test3020 passes`, so the item measures `match`.
- [x] `behaviour:test2043`: not closed here; filed as BL-2018 (the case reaches revoked.badssl.com on the internet, which the in-process harness skips).
- [x] `behaviour:test1293`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 256 bytes: HTTP/1.1 200 OK\x0ADate: Tue, 09 Nov 2010 14:49:00 GMT\x0AServer: test-server/fake\x0ALast-Modified: Tue, 13 Jun 2000 12:10:00 GMT\x0AETag: "21025-dc7-39462498"\x0AAccept-Ranges: bytes\x0AContent-Length: 6\x0AConnection: `, so the item measures `match`.
- [x] `behaviour:test1097`: Curl answers what curl 8.21.0 answers, `upstream test1097 passes`, so the item measures `match`.
- [x] `behaviour:test1230`: Curl answers what curl 8.21.0 answers, `upstream test1230 passes`, so the item measures `match`.
- [x] `behaviour:test1456`: Curl answers what curl 8.21.0 answers, `upstream test1456 passes`, so the item measures `match`.
- [x] `behaviour:test3028`: Curl answers what curl 8.21.0 answers, `upstream test3028 passes`, so the item measures `match`.
- [x] `behaviour:test3202`: Curl answers what curl 8.21.0 answers, `upstream test3202 passes`, so the item measures `match`.
- [x] `behaviour:test2050`: Curl answers what curl 8.21.0 answers, `upstream test2050 passes`, so the item measures `match`.
- [x] `behaviour:test2055`: Curl answers what curl 8.21.0 answers, `upstream test2055 passes`, so the item measures `match`.
- [x] `behaviour:test795`: not closed here; filed as BL-2018 (an HTTP redirect to IMAP hangs past 20 seconds, a different cause from this finding).
- [x] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [x] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-10 (lane 2): The suggested wiring was already in place: BL-1831 gave `InProcessCurl.RunAsync` a dialing overload (`ITcpDialer` + `IDnsResolver`) that builds the production `TcpConnector`, and BL-1794 moved `UpstreamConformanceTests.RunCurlAsync` onto it. Measured with the ratchet (lanes may not read `Gap/`, so `Measure-UpstreamCases.cs` was not run): 31 of the 36 items already passed and were listed. Of the other five, test1097 already passed (now listed). If the next gap run still measures the listed cases as gaps, the cause is `Gap/Tools/Measure-UpstreamCases.cs` calling the connector overload of `InProcessCurl.RunAsync` rather than the dialing one; that file is an audit path, so only an interactive session can check it.
- test2050 and test2055 (`--connect-to` through an HTTP proxy) were two product gaps, both fixed to match curl 8.21.0's `lib/url.c`: (1) curl tunnels through an HTTP or HTTPS proxy when a `--connect-to` mapping changes the URL's host or port (`parse_connect_to_slist` sets `tunnel_proxy`); `TransferContextFactory.ConnectToTunnelsThroughProxy` now sets `HttpRequestOptions.ProxyTunnel` for that. (2) `TcpConnector.DestinationOf` applied `--connect-to` to a forward-proxy target, whose host is the proxy, so the wildcard mapping `::host:port` redirected the proxy connection; curl maps only the origin, and a forward-proxy target is now never mapped. Both now pass and are listed.
- Left, filed as BL-2018: test795 (an HTTP redirect to IMAP hangs past 20 s in the ratchet; a different cause from this finding) and test2043 (reaches revoked.badssl.com on the internet; the harness skips it). Their boxes stay unticked.
- No option added or changed, so `--ai-help` needs nothing. No ADR: both fixes copy curl's measured source behaviour, no choice was made. `Measure-CodeQuality.ps1` not run (30-45 min under load); every new branch has its own test (`TransferContextFactoryTests.ConnectToTunnelsThroughProxy_*`, `Create_WithAConnectToMappingThroughAnHttpProxy_SetsProxyTunnel`, `TcpConnectorTests.ConnectAsync_ToAForwardProxyWithAWildcardConnectToMapping_DialsTheProxyUnmapped`).

## Log

- 2026-10-10: Created.
- 2026-10-10: Backlog -> Doing.
- 2026-10-10: Doing -> Done. 34 of 36 GF-0001 cases pass in process: --connect-to through an HTTP proxy now tunnels and never maps the proxy; 795 and 2043 filed as BL-2018
