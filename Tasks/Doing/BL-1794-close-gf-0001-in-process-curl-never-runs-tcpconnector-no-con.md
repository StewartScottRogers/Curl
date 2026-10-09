---
id: BL-1794
title: Close GF-0001: In process, Curl never runs TcpConnector: no CONNECT tunnel, no HAProxy header, no .onion refusal, no name-resolution or --resolve/--connect-to failures
priority: High
assignee: Claude
pipeline: feature
depends-on: [BL-1831]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-08
completed:
---
# BL-1794 — Close GF-0001: In process, Curl never runs TcpConnector: no CONNECT tunnel, no HAProxy header, no .onion refusal, no name-resolution or --resolve/--connect-to failures

## Goal

Curl behaves as curl 8.21.0 does for every item of gap finding GF-0001 (In process, Curl never runs TcpConnector: no CONNECT tunnel, no HAProxy header, no .onion refusal, no name-resolution or --resolve/--connect-to failures), so a later gap analysis measures each of `behaviour:test1008`, `behaviour:test1021`, `behaviour:test1060`, `behaviour:test1061`, `behaviour:test206`, `behaviour:test209`, `behaviour:test213`, `behaviour:test217`, `behaviour:test265`, `behaviour:test287`, `behaviour:test718`, `behaviour:test749`, `behaviour:test750`, `behaviour:test1715`, `behaviour:test94`, `behaviour:test440`, `behaviour:test441`, `behaviour:test493`, `behaviour:test1455`, `behaviour:test3201`, `behaviour:test3220`, `behaviour:test1471`, `behaviour:test1472`, `behaviour:test20`, `behaviour:test3019`, `behaviour:test3020`, `behaviour:test2043`, `behaviour:test1293` as `match`.

## Context

- Finding: GF-0001, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test1008`, `behaviour:test1021`, `behaviour:test1060`, `behaviour:test1061`, `behaviour:test206`, `behaviour:test209`, `behaviour:test213`, `behaviour:test217`, `behaviour:test265`, `behaviour:test287`, `behaviour:test718`, `behaviour:test749`, `behaviour:test750`, `behaviour:test1715`, `behaviour:test94`, `behaviour:test440`, `behaviour:test441`, `behaviour:test493`, `behaviour:test1455`, `behaviour:test3201`, `behaviour:test3220`, `behaviour:test1471`, `behaviour:test1472`, `behaviour:test20`, `behaviour:test3019`, `behaviour:test3020`, `behaviour:test2043`, `behaviour:test1293`.
- Targeted curl version: 8.21.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.21.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

Every item expects 'upstream test<N> passes'. test287 actual: <verify><protocol> differs at byte 0 (line 1): expected 'CONNECT test.remote.example.com.287:8990 HTTP/1.1', got 'GET /path/287 HTTP/1.1'. The same shape for the other -p/--proxytunnel and HTTPS-through-proxy cases (test94, test440, test441 and test493 expect CONNECT ...:443). test1455/3201/3220: expected 'proxy-line', got 'GET /<N> HTTP/1.1'. test1471/1472: <verify><stderr> expected 'curl: (6) Not resolving .onion address (RFC 7686)', got the progress meter. test20: expected exit code 6, got 52. test3019/3020: expected exit code 49, got 52. test2043: expected exit code 0, got 52. test1293: expected 'POST /1293', got 'POST /' (the first URL, http://0, reached the server). Cause in code: Curl.Console/InProcessCurl.RunAsync calls CurlComposition.CreateRunner with the caller's IConnector and no ConnectionCache, so the SwsHttpServerConnector receives every ConnectTarget, Proxy included, and connects directly. The CONNECT tunnel (Curl.Networking.UnitLibrary/HttpProxyTunnel via TcpConnector), HaproxyProtocolHeader, OnionAddress.IsRefused, name resolution and the --resolve/--connect-to parsing all live in TcpConnector, which this path never builds. HttpProtocolHandler.ForwardProxyOf hands every tunnelled proxy to the connector. Reproduce from the repository root (the output folder must exist and hold no blank): dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 287,1455,1471,20,3019,94

Suggestion, copied from the finding:

In Curl.Console, give InProcessCurl (and the CurlComposition.CreateRunner overload it calls) a way to run over the production Curl.Networking.UnitLibrary TcpConnector: take a dialer seam (the TCP dial and the name resolver) rather than a finished IConnector, and build TcpConnector with the run's HttpProxyTunnelOptions, HaproxyProtocolHeader, --resolve/--connect-to overrides and TLS provider, exactly as CreateTcpConnector does for the executable. Then a CONNECT, a PROXY line, the .onion refusal (exit 6), an unresolvable name (exit 6) and a bad --resolve/--connect-to entry (exit 49) reach the in-process server as they reach a real one. Change UpstreamConformanceTests.RunCurlAsync the same way so the ratchet measures the same path. If, with that wiring, a case still fails, the remaining difference is a product gap in TcpConnector and should be filed from the next run.

## Acceptance criteria

- [ ] `behaviour:test1008`: Curl answers what curl 8.21.0 answers, `upstream test1008 passes`, so the item measures `match`.
- [ ] `behaviour:test1021`: Curl answers what curl 8.21.0 answers, `upstream test1021 passes`, so the item measures `match`.
- [ ] `behaviour:test1060`: Curl answers what curl 8.21.0 answers, `upstream test1060 passes`, so the item measures `match`.
- [ ] `behaviour:test1061`: Curl answers what curl 8.21.0 answers, `upstream test1061 passes`, so the item measures `match`.
- [ ] `behaviour:test206`: Curl answers what curl 8.21.0 answers, `upstream test206 passes`, so the item measures `match`.
- [ ] `behaviour:test209`: Curl answers what curl 8.21.0 answers, `upstream test209 passes`, so the item measures `match`.
- [ ] `behaviour:test213`: Curl answers what curl 8.21.0 answers, `upstream test213 passes`, so the item measures `match`.
- [ ] `behaviour:test217`: Curl answers what curl 8.21.0 answers, `upstream test217 passes`, so the item measures `match`.
- [ ] `behaviour:test265`: Curl answers what curl 8.21.0 answers, `upstream test265 passes`, so the item measures `match`.
- [ ] `behaviour:test287`: Curl answers what curl 8.21.0 answers, `upstream test287 passes`, so the item measures `match`.
- [ ] `behaviour:test718`: Curl answers what curl 8.21.0 answers, `upstream test718 passes`, so the item measures `match`.
- [ ] `behaviour:test749`: Curl answers what curl 8.21.0 answers, `upstream test749 passes`, so the item measures `match`.
- [ ] `behaviour:test750`: Curl answers what curl 8.21.0 answers, `upstream test750 passes`, so the item measures `match`.
- [ ] `behaviour:test1715`: Curl answers what curl 8.21.0 answers, `upstream test1715 passes`, so the item measures `match`.
- [ ] `behaviour:test94`: Curl answers what curl 8.21.0 answers, `upstream test94 passes`, so the item measures `match`.
- [ ] `behaviour:test440`: Curl answers what curl 8.21.0 answers, `upstream test440 passes`, so the item measures `match`.
- [ ] `behaviour:test441`: Curl answers what curl 8.21.0 answers, `upstream test441 passes`, so the item measures `match`.
- [ ] `behaviour:test493`: Curl answers what curl 8.21.0 answers, `upstream test493 passes`, so the item measures `match`.
- [ ] `behaviour:test1455`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test3201`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test3220`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 0 bytes: `, so the item measures `match`.
- [ ] `behaviour:test1471`: Curl answers what curl 8.21.0 answers, `upstream test1471 passes`, so the item measures `match`.
- [ ] `behaviour:test1472`: Curl answers what curl 8.21.0 answers, `upstream test1472 passes`, so the item measures `match`.
- [ ] `behaviour:test20`: Curl answers what curl 8.21.0 answers, `upstream test20 passes`, so the item measures `match`.
- [ ] `behaviour:test3019`: Curl answers what curl 8.21.0 answers, `upstream test3019 passes`, so the item measures `match`.
- [ ] `behaviour:test3020`: Curl answers what curl 8.21.0 answers, `upstream test3020 passes`, so the item measures `match`.
- [ ] `behaviour:test2043`: Curl answers what curl 8.21.0 answers, `upstream test2043 passes`, so the item measures `match`.
- [ ] `behaviour:test1293`: Curl answers what curl 8.21.0 answers, `reference curl exits 0; stdout 256 bytes: HTTP/1.1 200 OK\x0ADate: Tue, 09 Nov 2010 14:49:00 GMT\x0AServer: test-server/fake\x0ALast-Modified: Tue, 13 Jun 2000 12:10:00 GMT\x0AETag: "21025-dc7-39462498"\x0AAccept-Ranges: bytes\x0AContent-Length: 6\x0AConnection: `, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

- 2026-10-08 (lane 1): Split. The run had a $2 cost cap, too small for the whole change. The wiring half (a TCP-dial and name-resolver seam so `InProcessCurl` runs `CurlComposition.CreateTcpConnector`, as built in `CreateTransports`) is BL-1831. What is left here: switch `Curl.Conformance.UnitTests` `UpstreamConformanceTests.RunCurlAsync` to that path, rerun the 28 upstream cases with `Gap/Tools/Measure-UpstreamCases.cs`, and file any case that still fails as a `TcpConnector` product gap.

## Log

- 2026-10-08: Created.
- 2026-10-08: Backlog -> Doing.
- 2026-10-08: Doing -> Backlog. Waits on BL-1831 (in-process TcpConnector wiring), split out because the run's cost cap could not fit the whole change
- 2026-10-08: Backlog -> Doing.
