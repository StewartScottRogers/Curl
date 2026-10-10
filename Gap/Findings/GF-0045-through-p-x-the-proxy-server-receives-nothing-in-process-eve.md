---
id: GF-0045
title: Through -p/-x the proxy server receives nothing in process: every <verify><proxy> case records no CONNECT
area: behaviour
key: behaviour:in-process-runner-never-reaches-proxy-server
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-10_0657
closed:
regression: false
items: [behaviour:test80, behaviour:test83, behaviour:test95, behaviour:test275, behaviour:test744, behaviour:test1078, behaviour:test1184, behaviour:test1288, behaviour:test1297, behaviour:test1428, behaviour:test1904, behaviour:test1319, behaviour:test1320, behaviour:test1321, behaviour:test2107]
touches: [Curl.Networking.UnitLibrary, Curl.Networking.UnitTests, Curl.Console, Curl.Console.UnitTests]
task:
tasks: []
---
# GF-0045 - Through -p/-x the proxy server receives nothing in process: every <verify><proxy> case records no CONNECT

## Summary

Curl differs from upstream curl in behaviour: Through -p/-x the proxy server receives nothing in process: every <verify><proxy> case records no CONNECT.

## Evidence

Every item expects 'upstream test<N> passes'. test80: '<verify><proxy> differs at byte 0 (line 1): expected "CONNECT test.80:8990 HTTP/1.0\r\n", got the end'. test1078 expected 'CONNECT 127.0.0.1:8990 HTTP/1.0', got the end. test1319/1320/1321 (pop3/smtp/imap with -p -x) expected 'CONNECT pop.1319:8999 HTTP/1.1' (and smtp., imap.), got the end. test2107 (-p -x to a proxy that answers badly): expected exit code 8, got 0. The same cause as behaviour:in-process-runner-bypasses-tcp-connector: the IConnector overload sends the proxied request straight to the origin server, so the harness's proxy server log stays empty. Listed as passing in Curl.Conformance.UnitTests/PassingUpstreamCases.txt: 80, 83, 95, 275, 744, 1078, 1184, 1297, 1428, 1904. Not listed: 1288, 1319, 1320, 1321, 2107. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 80,1078,1288,1319,2107

## Suggestion

Re-measure after the InProcessCurl rewiring of behaviour:in-process-runner-bypasses-tcp-connector. Then fix whatever still differs in Curl.Networking.UnitLibrary's HttpProxyTunnel. The cases are --suppress-connect-headers with -D - -i and %{http_connect} (test1288), a CONNECT tunnel for pop3, smtp and imap URLs (test1319-1321), and exit 8 for a proxy's malformed CONNECT reply (test2107). Pin each in Curl.Networking.UnitTests.

## Measurements

- 2026-10-10_0657: 15 of 15 items are gaps.

## Log

- 2026-10-10_0657: Opened by gap-behaviour.
