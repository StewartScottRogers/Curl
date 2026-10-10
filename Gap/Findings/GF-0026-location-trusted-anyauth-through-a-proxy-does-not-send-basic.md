---
id: GF-0026
title: --location-trusted --anyauth through a proxy does not send Basic credentials to the redirect's new host
area: behaviour
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

## Log

- 2026-10-08_1640: Opened by gap-behaviour.
- 2026-10-08_1731: Filed BL-1819.
- 2026-10-08_2029: Closed: run 2026-10-08_2029 measured every item as match or excluded.
