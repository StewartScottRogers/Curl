---
id: AF-0181
title: OtherPorts_ReachTheWrappedServer asserts only that some non-MQTT connection exists, not that it is the wrapped server's
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Conformance.UnitTests/MqttServerConnectorTests.cs:OtherPorts_ReachTheWrappedServer:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-10-13
found-at: c86d04eeb8477108eb6d9195bb036a4ddaecc5fc
scorecard: 2026-10-13_0925.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0181 - OtherPorts_ReachTheWrappedServer asserts only that some non-MQTT connection exists, not that it is the wrapped server's

## Summary

Low finding from the quality auditor at `Curl.Conformance.UnitTests/MqttServerConnectorTests.cs:198`: OtherPorts_ReachTheWrappedServer asserts only that some non-MQTT connection exists, not that it is the wrapped server's. Reported by an auditor flagged unreliable in 2026-10-13_0925.md.

## Evidence

Location: `Curl.Conformance.UnitTests/MqttServerConnectorTests.cs:198`

The test's only checks are 'Assert.IsNotNull(result.Connection);' and 'Assert.IsNotInstanceOfType<MqttServerConnection>(result.Connection);'. The name promises the wrapped SwsHttpServerConnector's connection. A connector that returned any other connection on port 8990 would pass: a different fake, or a backend reached on the wrong port. The sibling NoListenPortConnectorTests.ConnectAsync_OtherPort_ReachesTheWrappedServer (AF-0175) now asserts IsInstanceOfType<SwsHttpServerConnection> and RemoteEndPoint 127.0.0.1:8990; this test does neither.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.UnitTests/MqttServerConnectorTests.cs -Pattern "Assert\.IsNotNull\(result\.Connection\)"
```

- Expected: No match: the test asserts the connection is an SwsHttpServerConnection to 127.0.0.1:8990.
- Actual: MqttServerConnectorTests.cs:198:        Assert.IsNotNull(result.Connection);

## Re-audits

## Log

- 2026-10-13: filed proposed.
