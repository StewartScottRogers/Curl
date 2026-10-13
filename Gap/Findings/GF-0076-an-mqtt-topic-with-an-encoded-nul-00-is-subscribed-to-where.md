---
id: GF-0076
title: An MQTT topic with an encoded NUL (%00) is subscribed to, where curl fails with exit 3 before SUBSCRIBE
area: behaviour
key: behaviour:mqtt-topic-control-byte-not-rejected
severity: High
status: open
scope: target
introduced-in:
opened: 2026-10-13_1406
closed:
regression: false
items: [behaviour:test2109]
touches: [Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests]
task:
tasks: []
---
# GF-0076 - An MQTT topic with an encoded NUL (%00) is subscribed to, where curl fails with exit 3 before SUBSCRIBE

## Summary

Curl differs from upstream curl in behaviour: An MQTT topic with an encoded NUL (%00) is subscribed to, where curl fails with exit 3 before SUBSCRIBE.

## Evidence

behaviour:test2109 (mqtt://%HOSTIP:%MQTTPORT/aa%00bb): expected 'upstream test2109 passes' (CONNECT and CONNACK only, exit 3), actual '<verify><protocol> differs at byte 77 (line 3): expected the end, got client SUBSCRIBE a 00010005616100626200'. MqttSession uses MqttTopic.Decode(url) (Curl.Protocol.Mqtt.UnitLibrary/MqttSession.cs line 296) without checking the decoded bytes. Reproduce: dotnet run --file Gap/Tools/Measure-UpstreamCases.cs -- $env:LOCALAPPDATA/Curl/gap/upstream/8.22.0/tests/data $env:TEMP/raw.json 2109

## Suggestion

In Curl.Protocol.Mqtt.UnitLibrary, make MqttTopic.Decode (or MqttSession before SUBSCRIBE and PUBLISH) reject a decoded topic holding a control byte, NUL included, with exit 3 (CurlExitCode.UrlMalformat) after CONNACK and before SUBSCRIBE is written, as curl 8.22.0 does; take the message from the reference with Record-CurlExchange.ps1. Add unit tests for %00 and %0A in the topic.

## Measurements

- 2026-10-13_1406: 1 of 1 items are gaps.

## Log

- 2026-10-13_1406: Opened by gap-behaviour.
