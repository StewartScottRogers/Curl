---
id: GF-0071
title: A custom 'Authorization : value' header (white space before the colon) is still sent after -L redirects to another host
area: behaviour
platform: windows
key: behaviour:custom-authorization-header-with-space-before-colon-kept-on-redirect
severity: Critical
status: closed
scope: target
introduced-in:
opened: 2026-10-13_1406
closed: 2026-10-14_0904
regression: false
items: [behaviour:test2113]
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
task: BL-2163
tasks: [BL-2163]
---
# GF-0071 - A custom 'Authorization : value' header (white space before the colon) is still sent after -L redirects to another host

## Summary

Curl differs from upstream curl in behaviour: A custom 'Authorization : value' header (white space before the colon) is still sent after -L redirects to another host.

## Evidence

behaviour:test2113 expected 'upstream test2113 passes', actual '<verify><protocol> differs at byte 416 (line 15): expected <CR><LF>, got Authorization : s3cr3t<CR><LF>'. Command: http://first.host.it.is/we/want/that/page/2113 -x %HOSTIP:%HTTPPORT -H 'Authorization : s3cr3t' --proxy-user testing:this --location; the 302 points to http://goto.second.host.now/21130002 and upstream drops the header from the second request. HttpCustomHeader.Names (Curl.Protocol.Http.UnitLibrary/HttpCustomHeader.cs line 71) requires the colon right after the name (separatorIndex == name.Length), so 'Authorization :' is not recognised as Authorization. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 2113

## Suggestion

In Curl.Protocol.Http.UnitLibrary, make HttpCustomHeader.Names compare the header name with the white space before its colon (or semicolon) trimmed, so every check that asks whether an -H entry names Authorization, Cookie, Host and the like - the strip on a redirect to another host without --location-trusted included - treats 'Authorization : x' as Authorization, as curl 8.22.0 does. Check RtspCustomHeader.Names and WsCustomHeader.Names for the same comparison. Add a unit test for the redirect strip with 'Authorization :'.

## Measurements

- 2026-10-13_1406: 1 of 1 items are gaps.
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

- 2026-10-13_1406: Opened by gap-behaviour.
- 2026-10-13_1457: Filed BL-2163.
- 2026-10-14_0904: Closed: run 2026-10-14_0904 measured every item as match or excluded.
