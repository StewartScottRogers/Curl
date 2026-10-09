---
id: GF-0001
title: In process, Curl never runs TcpConnector: no CONNECT tunnel, no HAProxy header, no .onion refusal, no name-resolution or --resolve/--connect-to failures
area: behaviour
key: behaviour:in-process-runner-bypasses-tcp-connector
severity: Critical
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test1008, behaviour:test1021, behaviour:test1060, behaviour:test1061, behaviour:test206, behaviour:test209, behaviour:test213, behaviour:test217, behaviour:test265, behaviour:test287, behaviour:test718, behaviour:test749, behaviour:test750, behaviour:test1715, behaviour:test94, behaviour:test440, behaviour:test441, behaviour:test493, behaviour:test1455, behaviour:test3201, behaviour:test3220, behaviour:test1471, behaviour:test1472, behaviour:test20, behaviour:test3019, behaviour:test3020, behaviour:test2043, behaviour:test1293]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Conformance.UnitTests]
task: BL-1794
tasks: [BL-1794]
---
# GF-0001 - In process, Curl never runs TcpConnector: no CONNECT tunnel, no HAProxy header, no .onion refusal, no name-resolution or --resolve/--connect-to failures

## Summary

Curl differs from upstream curl in behaviour: In process, Curl never runs TcpConnector: no CONNECT tunnel, no HAProxy header, no .onion refusal, no name-resolution or --resolve/--connect-to failures.

## Evidence

Every item expects 'upstream test<N> passes'. test287 actual: <verify><protocol> differs at byte 0 (line 1): expected 'CONNECT test.remote.example.com.287:8990 HTTP/1.1', got 'GET /path/287 HTTP/1.1'. The same shape for the other -p/--proxytunnel and HTTPS-through-proxy cases (test94, test440, test441 and test493 expect CONNECT ...:443). test1455/3201/3220: expected 'proxy-line', got 'GET /<N> HTTP/1.1'. test1471/1472: <verify><stderr> expected 'curl: (6) Not resolving .onion address (RFC 7686)', got the progress meter. test20: expected exit code 6, got 52. test3019/3020: expected exit code 49, got 52. test2043: expected exit code 0, got 52. test1293: expected 'POST /1293', got 'POST /' (the first URL, http://0, reached the server). Cause in code: Curl.Console/InProcessCurl.RunAsync calls CurlComposition.CreateRunner with the caller's IConnector and no ConnectionCache, so the SwsHttpServerConnector receives every ConnectTarget, Proxy included, and connects directly. The CONNECT tunnel (Curl.Networking.UnitLibrary/HttpProxyTunnel via TcpConnector), HaproxyProtocolHeader, OnionAddress.IsRefused, name resolution and the --resolve/--connect-to parsing all live in TcpConnector, which this path never builds. HttpProtocolHandler.ForwardProxyOf hands every tunnelled proxy to the connector. Reproduce from the repository root (the output folder must exist and hold no blank): dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 287,1455,1471,20,3019,94

## Suggestion

In Curl.Console, give InProcessCurl (and the CurlComposition.CreateRunner overload it calls) a way to run over the production Curl.Networking.UnitLibrary TcpConnector: take a dialer seam (the TCP dial and the name resolver) rather than a finished IConnector, and build TcpConnector with the run's HttpProxyTunnelOptions, HaproxyProtocolHeader, --resolve/--connect-to overrides and TLS provider, exactly as CreateTcpConnector does for the executable. Then a CONNECT, a PROXY line, the .onion refusal (exit 6), an unresolvable name (exit 6) and a bad --resolve/--connect-to entry (exit 49) reach the in-process server as they reach a real one. Change UpstreamConformanceTests.RunCurlAsync the same way so the ratchet measures the same path. If, with that wiring, a case still fails, the remaining difference is a product gap in TcpConnector and should be filed from the next run.

## Measurements

- 2026-10-08_1640: 28 of 28 items are gaps.
- 2026-10-08_2029: 28 of 28 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1794.
