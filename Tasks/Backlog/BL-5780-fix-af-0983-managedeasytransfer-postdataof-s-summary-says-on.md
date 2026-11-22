---
id: BL-5780
title: Fix AF-0983: ManagedEasyTransfer.PostDataOf's summary says only an mqtt or mqtts URL reads the post data; it also gives it to every extension URL, read-callback posts included
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5780 — Fix AF-0983: ManagedEasyTransfer.PostDataOf's summary says only an mqtt or mqtts URL reads the post data; it also gives it to every extension URL, read-callback posts included

## Goal

The defect the audit office reported as AF-0983 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0983 (Medium, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:252`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs:252`

Lines 249-252 document PostDataOf as 'The CURLOPT_POSTFIELDS an MQTT transfer publishes ... or null to subscribe instead; only an mqtt or mqtts URL reads it, as libcurl's mqtt_do does'. The body (line 257) is 'options.Http.PostFields is { } fields ? PostFieldsDataOf(options, fields) : ReadCallbackPostDataOf(options)'. PostFieldsDataOf (line 265) returns the fields for 'IsMqttUrl(options) || IsExtensionUrl(options)': every scheme curl itself does not serve (BL-5758), and IsMqttUrl covers mqtt5, mqtt5s and mqtt5+ws too. ReadCallbackPostDataOf reads a CURLOPT_READFUNCTION post to an extension URL up front (BL-5754). ADR-1336 and ADR-1337 decided both widenings. The summary still describes the MQTT-only member it used to be.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitLibrary/ManagedEasyTransfer.Context.cs -Pattern 'only an <c>mqtt</c> or <c>mqtts</c> URL reads it|IsMqttUrl\(options\) \|\| IsExtensionUrl|: ReadCallbackPostDataOf'
```

- Expected: PostDataOf's summary names MQTT and extension URLs and read-callback posts; no 'only an mqtt or mqtts URL reads it' match.
- Actual: ManagedEasyTransfer.Context.cs:252: ... only an <c>mqtt</c> or <c>mqtts</c> URL reads it ...
ManagedEasyTransfer.Context.cs:257: ... : ReadCallbackPostDataOf(options);
ManagedEasyTransfer.Context.cs:265:        if (!(IsMqttUrl(options) || IsExtensionUrl(options)))

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-22: Created.
