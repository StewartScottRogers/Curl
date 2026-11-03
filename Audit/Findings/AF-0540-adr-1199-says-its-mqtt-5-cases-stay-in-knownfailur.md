---
id: AF-0540
title: ADR-1199 says its MQTT 5 cases stay in KnownFailures.txt under BL-4580, BL-4581 and BL-4582; the file lists them under BL-4573, BL-4574 and BL-4575
auditor: truthfulness
severity: Medium
status: accepted
reason: 
key: truthfulness:Documentation/Planning/Decisions/ADR-1199-mqtt5-acknowledges-received-qos-refuses-unallowed-topic-aliases-and-closes-on-a-missing-pingresp.md:KnownFailures-task-ids:stale-adr
reproduction: none
task: BL-4646
tasks: BL-4646
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0540 - ADR-1199 says its MQTT 5 cases stay in KnownFailures.txt under BL-4580, BL-4581 and BL-4582; the file lists them under BL-4573, BL-4574 and BL-4575

## Summary

Medium finding from the truthfulness auditor at `Documentation/Planning/Decisions/ADR-1199-mqtt5-acknowledges-received-qos-refuses-unallowed-topic-aliases-and-closes-on-a-missing-pingresp.md:28`: ADR-1199 says its MQTT 5 cases stay in KnownFailures.txt under BL-4580, BL-4581 and BL-4582; the file lists them under BL-4573, BL-4574 and BL-4575. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Documentation/Planning/Decisions/ADR-1199-mqtt5-acknowledges-received-qos-refuses-unallowed-topic-aliases-and-closes-on-a-missing-pingresp.md:28`

ADR-1199 Consequences: 'The cases stay in KnownFailures.txt under BL-4580, BL-4581 and BL-4582 until each fix lands'. Curl.Conformance.Extended.UnitTests/KnownFailures.txt lists the ADR's requirements as 'Mqtt5:MQTT-4.3.2-4 BL-4573', 'Mqtt5:MQTT-4.6.0-2 BL-4573', 'Mqtt5:MQTT-4.3.3-8 BL-4573', 'Mqtt5:MQTT-4.3.3-11 BL-4573', 'Mqtt5:MQTT-4.6.0-3 BL-4573' (QoS acknowledgement), 'Mqtt5:MQTT5-3.3.2.3.4-1 BL-4574' (Topic Alias) and 'Mqtt5:MQTT5-3.1.2.10-1 BL-4575' (PINGRESP). BL-4580, BL-4581 and BL-4582 appear nowhere in the file. An agent looking for the task that fixes these cases would look for the wrong IDs.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitTests/KnownFailures.txt -Pattern 'BL-458[012]|MQTT-4.3.2-4|MQTT5-3.3.2.3.4-1|MQTT5-3.1.2.10-1'
```

- Expected: The three requirement lines carry BL-4580, BL-4581 and BL-4582, as ADR-1199 states (or the ADR names the IDs the file uses).
- Actual: KnownFailures.txt:9:Mqtt5:MQTT-4.3.2-4 BL-4573 / :14:Mqtt5:MQTT5-3.3.2.3.4-1 BL-4574 / :15:Mqtt5:MQTT5-3.1.2.10-1 BL-4575; no line names BL-4580, BL-4581 or BL-4582.

## Re-audits

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
