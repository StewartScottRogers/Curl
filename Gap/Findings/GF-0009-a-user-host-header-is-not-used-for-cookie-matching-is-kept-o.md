---
id: GF-0009
title: A user Host: header is not used for cookie matching, is kept on a redirect to another host, and a lower-case 'host:' does not remove it
area: behaviour
key: behaviour:custom-host-header-handling
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-08_1640
closed:
regression: false
items: [behaviour:test62, behaviour:test1258, behaviour:test184, behaviour:test461]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests, Curl.Cookies.UnitLibrary, Curl.Cookies.UnitTests]
task:
tasks: []
---
# GF-0009 - A user Host: header is not used for cookie matching, is kept on a redirect to another host, and a lower-case 'host:' does not remove it

## Summary

Curl differs from upstream curl in behaviour: A user Host: header is not used for cookie matching, is kept on a redirect to another host, and a lower-case 'host:' does not remove it.

## Evidence

Every item expects 'upstream test<N> passes'. test62 actual: <verify><protocol> differs at byte 88 (line 5): expected 'Cookie: test2=yes; test=yes', got an empty line. test1258 (-H 'Host: localhost'): expected 'Cookie: I-am=here', got an empty line. test184 (-L -H 'Host: another.visitor...' through a proxy, Location to yet.another.host): expected 'Host: yet.another.host', got 'Host: another.visitor.stay.a.while.stay.foreeeeeever'. test461 (-H host:): expected 'User-Agent: curl/8.21.0', got 'Host:'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 62,1258,184,461

## Suggestion

In Curl.Protocol.Http.UnitLibrary (HttpCustomHeader, HttpRequestHeadFormatter) and the cookie lookup in Curl.Cookies.UnitLibrary: match header names case-insensitively, so '-H host:' removes Host. On the first request, use a custom Host header's name as the cookie host, as curl does. Drop the custom Host when -L follows to a different host (curl's 'this_is_a_follow' with a host change).

## Measurements

- 2026-10-08_1640: 4 of 4 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
