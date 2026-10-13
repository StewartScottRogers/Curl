---
id: BL-2163
title: Close GF-0071: A custom 'Authorization : value' header (white space before the colon) is still sent after -L redirects to another host
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2163 — Close GF-0071: A custom 'Authorization : value' header (white space before the colon) is still sent after -L redirects to another host

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0071 (A custom 'Authorization : value' header (white space before the colon) is still sent after -L redirects to another host), so a later gap analysis measures each of `behaviour:test2113` as `match`.

## Context

- Finding: GF-0071, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: Critical. Introduced in: not stated upstream.
- Items: `behaviour:test2113`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test2113 expected 'upstream test2113 passes', actual '<verify><protocol> differs at byte 416 (line 15): expected <CR><LF>, got Authorization : s3cr3t<CR><LF>'. Command: http://first.host.it.is/we/want/that/page/2113 -x %HOSTIP:%HTTPPORT -H 'Authorization : s3cr3t' --proxy-user testing:this --location; the 302 points to http://goto.second.host.now/21130002 and upstream drops the header from the second request. HttpCustomHeader.Names (Curl.Protocol.Http.UnitLibrary/HttpCustomHeader.cs line 71) requires the colon right after the name (separatorIndex == name.Length), so 'Authorization :' is not recognised as Authorization. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 2113

Suggestion, copied from the finding:

In Curl.Protocol.Http.UnitLibrary, make HttpCustomHeader.Names compare the header name with the white space before its colon (or semicolon) trimmed, so every check that asks whether an -H entry names Authorization, Cookie, Host and the like - the strip on a redirect to another host without --location-trusted included - treats 'Authorization : x' as Authorization, as curl 8.22.0 does. Check RtspCustomHeader.Names and WsCustomHeader.Names for the same comparison. Add a unit test for the redirect strip with 'Authorization :'.

## Acceptance criteria

- [ ] `behaviour:test2113`: Curl answers what curl 8.22.0 answers, `upstream test2113 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-13: Created.
