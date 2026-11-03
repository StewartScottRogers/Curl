---
id: BL-4646
title: Fix AF-0540: ADR-1199 says its MQTT 5 cases stay in KnownFailures.txt under BL-4580, BL-4581 and BL-4582; the file lists them under BL-4573, BL-4574 and BL-4575
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation/Planning/Decisions/ADR-1199-mqtt5-acknowledges-received-qos-refuses-unallowed-topic-aliases-and-closes-on-a-missing-pingresp.md]
requirement: none
created: 2026-11-03
completed: 2026-11-03
---
# BL-4646 — Fix AF-0540: ADR-1199 says its MQTT 5 cases stay in KnownFailures.txt under BL-4580, BL-4581 and BL-4582; the file lists them under BL-4573, BL-4574 and BL-4575

## Goal

The defect the audit office reported as AF-0540 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0540 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0540-adr-1199-says-its-mqtt-5-cases-stay-in-knownfailur.md`.

Location: `Documentation/Planning/Decisions/ADR-1199-mqtt5-acknowledges-received-qos-refuses-unallowed-topic-aliases-and-closes-on-a-missing-pingresp.md:28`

Location: `Documentation/Planning/Decisions/ADR-1199-mqtt5-acknowledges-received-qos-refuses-unallowed-topic-aliases-and-closes-on-a-missing-pingresp.md:28`

ADR-1199 Consequences: 'The cases stay in KnownFailures.txt under BL-4580, BL-4581 and BL-4582 until each fix lands'. Curl.Conformance.Extended.UnitTests/KnownFailures.txt lists the ADR's requirements as 'Mqtt5:MQTT-4.3.2-4 BL-4573', 'Mqtt5:MQTT-4.6.0-2 BL-4573', 'Mqtt5:MQTT-4.3.3-8 BL-4573', 'Mqtt5:MQTT-4.3.3-11 BL-4573', 'Mqtt5:MQTT-4.6.0-3 BL-4573' (QoS acknowledgement), 'Mqtt5:MQTT5-3.3.2.3.4-1 BL-4574' (Topic Alias) and 'Mqtt5:MQTT5-3.1.2.10-1 BL-4575' (PINGRESP). BL-4580, BL-4581 and BL-4582 appear nowhere in the file. An agent looking for the task that fixes these cases would look for the wrong IDs.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitTests/KnownFailures.txt -Pattern 'BL-458[012]|MQTT-4.3.2-4|MQTT5-3.3.2.3.4-1|MQTT5-3.1.2.10-1'
```

- Expected: The three requirement lines carry BL-4580, BL-4581 and BL-4582, as ADR-1199 states (or the ADR names the IDs the file uses).
- Actual: KnownFailures.txt:9:Mqtt5:MQTT-4.3.2-4 BL-4573 / :14:Mqtt5:MQTT5-3.3.2.3.4-1 BL-4574 / :15:Mqtt5:MQTT5-3.1.2.10-1 BL-4575; no line names BL-4580, BL-4581 or BL-4582.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

BL-4580, BL-4581 and BL-4582 are Done and KnownFailures.txt no longer lists the MQTT 5 cases, so ADR-1199 now says that in the past tense. Docs-only change; no code touched, so build and tests are unaffected.

## Log

- 2026-11-03: Created.
- 2026-11-03: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
- 2026-11-03: Doing -> Done. ADR-1199 Consequences now names the Done tasks and says the file no longer lists the cases
