---
id: GF-0002
title: In process, Curl opens a new connection for every request, so connection-reuse cases see [DISCONNECT], local port -1 and the wrong verdict for the second response
area: behaviour
key: behaviour:in-process-runner-has-no-connection-pool
severity: Critical
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test338, behaviour:test1421, behaviour:test1134, behaviour:test48, behaviour:test1418, behaviour:test1419, behaviour:test435, behaviour:test1074, behaviour:test1479, behaviour:test471]
touches: [Curl.Console, Curl.Console.UnitTests, Curl.Conformance.UnitLibrary, Curl.Conformance.UnitTests]
task: BL-1795
tasks: [BL-1795]
---
# GF-0002 - In process, Curl opens a new connection for every request, so connection-reuse cases see [DISCONNECT], local port -1 and the wrong verdict for the second response

## Summary

Curl differs from upstream curl in behaviour: In process, Curl opens a new connection for every request, so connection-reuse cases see [DISCONNECT], local port -1 and the wrong verdict for the second response.

## Evidence

Every item expects 'upstream test<N> passes'. test338 actual: <verify><protocol> differs at byte 81 (line 6): expected 'GET /338 HTTP/1.1', got '[DISCONNECT]'. test1421, test1134, test48, test1418 and test1419 have the same shape. test435: <verify><stdout> expected 'local port == [digits]', got 'local port == -1'; the reference curl printed 'local port == 55059'. test1074: expected 'GET /wantmore/10740001 HTTP/1.0', got '... HTTP/1.1' (the downgrade is kept on the reused connection). test1479: expected exit code 8, got 1. test471: expected exit code 8, got 0. Both need the second response on the same connection. Cause in code: InProcessCurl.RunAsync passes no ConnectionCache, so CurlComposition.GroupConnectorOf returns the bare connector and no PoolingConnector (ADR-0050) keeps a connection between requests or URLs. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 338,435,1074,1479

## Suggestion

In Curl.Console, make InProcessCurl.RunAsync create a run ConnectionCache and pass it to CurlComposition.CreateRunner (runConnections), so GroupConnectorOf wraps the connector in CreatePoolingConnector as the executable does. Report the fake connection's local end point from SwsHttpServerConnector's connections, so %{local_port} is a real number. Mirror the change in Curl.Conformance.UnitTests' RunCurlAsync. Re-measure: a case still failing after this is a product gap in PoolingConnector or HttpProtocolHandler's same-connection handling of HTTP/1.0 and HTTP/0.9 replies.

## Measurements

- 2026-10-08_1640: 10 of 10 items are gaps.
- 2026-10-08_2029: 1 of 10 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1795.
