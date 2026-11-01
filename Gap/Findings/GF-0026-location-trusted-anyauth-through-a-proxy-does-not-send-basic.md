---
id: GF-0026
title: --location-trusted --anyauth through a proxy does not send Basic credentials to the redirect's new host
area: behaviour
platform: windows
key: behaviour:location-trusted-anyauth-through-proxy
severity: Critical
status: closed
scope: target
introduced-in:
opened: 2026-10-08_1640
closed: 2026-10-08_2029
regression: false
items: [behaviour:test1088]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-1819
tasks: [BL-1819]
---
# GF-0026 - --location-trusted --anyauth through a proxy does not send Basic credentials to the redirect's new host

## Summary

Curl differs from upstream curl in behaviour: --location-trusted --anyauth through a proxy does not send Basic credentials to the redirect's new host.

## Evidence

behaviour:test1088 (-x ... --user iam:myself --location-trusted --anyauth) expected 'upstream test1088 passes', actual: <verify><protocol> differs at byte 436 (line 16): expected 'Authorization: Basic aWFtOm15c2VsZg==', got 'User-Agent: curl/8.21.0'. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- "$env:LOCALAPPDATA/Curl/gap/upstream/8.21.0/tests/data" C:/Temp/gap/raw.json 1088

## Suggestion

In Curl.Protocol.Http.UnitLibrary's redirect handling (HttpProtocolHandler with the authenticator), under --location-trusted carry the scheme --anyauth picked (Basic here) to the redirect's new host and send it pre-emptively. Do not start a new negotiation with no Authorization.

## Measurements

- 2026-10-08_1640: 1 of 1 items are gaps.
- 2026-10-08_2029: 0 of 1 items are gaps.
- 2026-10-10_0657: 0 of 1 items are gaps.
- 2026-10-11_0936: 0 of 1 items are gaps.
- 2026-10-11_1950: 0 of 1 items are gaps.
- 2026-10-12_0951: 0 of 1 items are gaps.
- 2026-10-12_2042: 0 of 1 items are gaps.
- 2026-10-13_1406: 0 of 1 items are gaps.
- 2026-10-14_0904: 0 of 1 items are gaps.
- 2026-10-15_0304: 0 of 1 items are gaps.
- 2026-10-15_0543: 0 of 1 items are gaps.
- 2026-10-15_0751: 0 of 1 items are gaps.
- 2026-10-15_1657: 0 of 1 items are gaps.
- 2026-10-17_2225: 0 of 1 items are gaps.
- 2026-10-19_0804: 0 of 1 items are gaps.
- 2026-10-19_1329: 0 of 1 items are gaps.
- 2026-10-22_0148: 0 of 1 items are gaps.
- 2026-10-23_1637: 0 of 1 items are gaps.
- 2026-10-25_1542: 0 of 1 items are gaps.
- 2026-10-30_2103: 0 of 1 items are gaps.
- 2026-10-31_1828: 0 of 1 items are gaps.

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1819.
- 2026-10-08_2029: Closed: run 2026-10-08_2029 measured every item as match or excluded.
