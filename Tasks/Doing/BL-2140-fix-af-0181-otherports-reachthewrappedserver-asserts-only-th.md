---
id: BL-2140
title: Fix AF-0181: OtherPorts_ReachTheWrappedServer asserts only that some non-MQTT connection exists, not that it is the wrapped server's
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2140 — Fix AF-0181: OtherPorts_ReachTheWrappedServer asserts only that some non-MQTT connection exists, not that it is the wrapped server's

## Goal

The defect the audit office reported as AF-0181 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0181 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0181-otherports-reachthewrappedserver-asserts-only-that.md`.

Location: `Curl.Conformance.UnitTests/MqttServerConnectorTests.cs:198`

Location: `Curl.Conformance.UnitTests/MqttServerConnectorTests.cs:198`

The test's only checks are 'Assert.IsNotNull(result.Connection);' and 'Assert.IsNotInstanceOfType<MqttServerConnection>(result.Connection);'. The name promises the wrapped SwsHttpServerConnector's connection. A connector that returned any other connection on port 8990 would pass: a different fake, or a backend reached on the wrong port. The sibling NoListenPortConnectorTests.ConnectAsync_OtherPort_ReachesTheWrappedServer (AF-0175) now asserts IsInstanceOfType<SwsHttpServerConnection> and RemoteEndPoint 127.0.0.1:8990; this test does neither.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.UnitTests/MqttServerConnectorTests.cs -Pattern "Assert\.IsNotNull\(result\.Connection\)"
```

- Expected: No match: the test asserts the connection is an SwsHttpServerConnection to 127.0.0.1:8990.
- Actual: MqttServerConnectorTests.cs:198:        Assert.IsNotNull(result.Connection);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-13: Backlog -> Doing.
