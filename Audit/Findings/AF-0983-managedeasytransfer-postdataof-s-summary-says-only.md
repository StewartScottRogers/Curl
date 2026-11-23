---
id: AF-0983
title: ManagedEasyTransfer.PostDataOf's summary says only an mqtt or mqtts URL reads the post data; it also gives it to every extension URL, read-callback posts included
auditor: truthfulness
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: truthfulness:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:PostDataOf:false-doc-comment
reproduction: none
task: BL-5780
tasks: BL-5780
found: 2026-11-22
found-at: fdfc7d13ff07752cdeb7bf999a56dd653660b7a2
scorecard: 2026-11-22_1351.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0983 - ManagedEasyTransfer.PostDataOf's summary says only an mqtt or mqtts URL reads the post data; it also gives it to every extension URL, read-callback posts included

## Summary

Medium finding from the truthfulness auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:252`: ManagedEasyTransfer.PostDataOf's summary says only an mqtt or mqtts URL reads the post data; it also gives it to every extension URL, read-callback posts included.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:252`

Lines 249-252 document PostDataOf as 'The CURLOPT_POSTFIELDS an MQTT transfer publishes ... or null to subscribe instead; only an mqtt or mqtts URL reads it, as libcurl's mqtt_do does'. The body (line 257) is 'options.Http.PostFields is { } fields ? PostFieldsDataOf(options, fields) : ReadCallbackPostDataOf(options)'. PostFieldsDataOf (line 265) returns the fields for 'IsMqttUrl(options) || IsExtensionUrl(options)': every scheme curl itself does not serve (BL-5758), and IsMqttUrl covers mqtt5, mqtt5s and mqtt5+ws too. ReadCallbackPostDataOf reads a CURLOPT_READFUNCTION post to an extension URL up front (BL-5754). ADR-1336 and ADR-1337 decided both widenings. The summary still describes the MQTT-only member it used to be.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs -Pattern 'only an <c>mqtt</c> or <c>mqtts</c> URL reads it|IsMqttUrl\(options\) \|\| IsExtensionUrl|: ReadCallbackPostDataOf'
```

- Expected: PostDataOf's summary names MQTT and extension URLs and read-callback posts; no 'only an mqtt or mqtts URL reads it' match.
- Actual: ManagedEasyTransfer.Context.cs:252: ... only an <c>mqtt</c> or <c>mqtts</c> URL reads it ...
ManagedEasyTransfer.Context.cs:257: ... : ReadCallbackPostDataOf(options);
ManagedEasyTransfer.Context.cs:265:        if (!(IsMqttUrl(options) || IsExtensionUrl(options)))

## Re-audits

## Log

- 2026-11-22: filed proposed.
