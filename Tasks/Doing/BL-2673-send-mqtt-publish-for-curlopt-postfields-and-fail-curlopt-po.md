---
id: BL-2673
title: Send MQTT PUBLISH for CURLOPT_POSTFIELDS and fail CURLOPT_POST without fields so upstream cases 1916 and 1917 pass
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Engine.UnitLibrary, Curl.Engine.UnitTests, Curl.Protocol.Mqtt.UnitLibrary, Curl.Protocol.Mqtt.UnitTests, Curl.Conformance.UnitTests/PassingUpstreamCases.txt]
requirement: none
created: 2026-10-17
completed:
---
# BL-2673 — Send MQTT PUBLISH for CURLOPT_POSTFIELDS and fail CURLOPT_POST without fields so upstream cases 1916 and 1917 pass

## Goal

An MQTT transfer with `CURLOPT_POSTFIELDS` (even empty) sends PUBLISH, and one with `CURLOPT_POST` and no post fields fails with `CURLE_BAD_FUNCTION_ARGUMENT` (43) after CONNACK, as libcurl does, and upstream cases 1916 and 1917 pass.

## Context

Found by BL-2545 (lib1916 port, which serves cases 1916 and 1917). lib1916 sets empty `CURLOPT_POSTFIELDS` (1916) or `CURLOPT_POST` 1 alone (1917) on `mqtt://%HOSTIP:%MQTTPORT/%20`. Case 1916's `<verify><protocol>` differs at byte 84 (line 3): expected `client PUBLISH 3 000120`, got `client SUBSCRIBE 6 000100012000`; case 1917's at byte 77 (line 3): expected the end (and exit 43), got `client SUBSCRIBE 6 000100012000`. The libtest path's post options are not reaching the MQTT handler's publish choice.

## Acceptance criteria

- [ ] Empty `CURLOPT_POSTFIELDS` on an MQTT URL publishes an empty payload, and `CURLOPT_POST` without fields fails with 43 after CONNACK, each with a focused unit test.
- [ ] Cases 1916 and 1917 pass through the runner on Windows and are added to `Curl.Conformance.UnitTests/PassingUpstreamCases.txt`.
- [ ] `dotnet build` is clean and the fast tests pass with no test deleted, skipped or weakened.

## Notes

## Log

- 2026-10-17: Created.
- 2026-10-19: Backlog -> Doing. Criteria at claim: 7cc27c32 c48c7d79 4272a6b9.
