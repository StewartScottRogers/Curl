---
id: BL-2168
title: Close GF-0076: An MQTT topic with an encoded NUL (%00) is subscribed to, where curl fails with exit 3 before SUBSCRIBE
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2168 — Close GF-0076: An MQTT topic with an encoded NUL (%00) is subscribed to, where curl fails with exit 3 before SUBSCRIBE

## Goal

Curl behaves as curl 8.22.0 does for every item of gap finding GF-0076 (An MQTT topic with an encoded NUL (%00) is subscribed to, where curl fails with exit 3 before SUBSCRIBE), so a later gap analysis measures each of `behaviour:test2109` as `match`.

## Context

- Finding: GF-0076, filed by the gap analysis office (ADR-0433).
- Area: behaviour. Severity: High. Introduced in: not stated upstream.
- Items: `behaviour:test2109`.
- Targeted curl version: 8.22.0 (Gap/Baselines/target.json).
- Yardstick: `tests/data/test*` in the curl 8.22.0 release tarball.
- The gap closes only when a later gap analysis re-measures every item as `match` (ADR-0433 decision 5), never because this task reaches Done.

Evidence, copied from the finding:

behaviour:test2109 (mqtt://%HOSTIP:%MQTTPORT/aa%00bb): expected 'upstream test2109 passes' (CONNECT and CONNACK only, exit 3), actual '<verify><protocol> differs at byte 77 (line 3): expected the end, got client SUBSCRIBE a 00010005616100626200'. MqttSession uses MqttTopic.Decode(url) (Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs line 296) without checking the decoded bytes. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 2109

Suggestion, copied from the finding:

In Curl.Protocol.Mqtt.UnitLibrary, make MqttTopic.Decode (or MqttSession before SUBSCRIBE and PUBLISH) reject a decoded topic holding a control byte, NUL included, with exit 3 (CurlExitCode.UrlMalformat) after CONNACK and before SUBSCRIBE is written, as curl 8.22.0 does; take the message from the reference with Record-CurlExchange.ps1. Add unit tests for %00 and %0A in the topic.

## Acceptance criteria

- [ ] `behaviour:test2109`: Curl answers what curl 8.22.0 answers, `upstream test2109 passes`, so the item measures `match`.
- [ ] `dotnet build -warnaserror` is clean and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) are green.
- [ ] When an option is added or changed, `curl --ai-help` is kept right (CLAUDE.md).

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
