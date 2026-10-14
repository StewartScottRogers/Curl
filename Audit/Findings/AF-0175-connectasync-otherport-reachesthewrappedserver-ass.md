---
id: AF-0175
title: ConnectAsync_OtherPort_ReachesTheWrappedServer asserts only that some connection exists, not that it is the wrapped server's
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces.
key: quality:Curl.Conformance.UnitTests/NoListenPortConnectorTests.cs:ConnectAsync_OtherPort_ReachesTheWrappedServer:weak-assertion
reproduction: none
task: BL-2121
tasks: BL-2121
found: 2026-10-12
found-at: 4e6ddbc5b33356117bfec6778ba550ab61454a99
scorecard: 2026-10-12_1653.md
duplicate-of:
closed: 2026-10-13
closed-how: reliable-reaudit
closed-by: 2026-10-13_1822.md
---
# AF-0175 - ConnectAsync_OtherPort_ReachesTheWrappedServer asserts only that some connection exists, not that it is the wrapped server's

## Summary

Low finding from the quality auditor at `Curl.Conformance.UnitTests/NoListenPortConnectorTests.cs:42`: ConnectAsync_OtherPort_ReachesTheWrappedServer asserts only that some connection exists, not that it is the wrapped server's. Reported by an auditor flagged unreliable in 2026-10-12_1653.md.

## Evidence

Location: `Curl.Conformance.UnitTests/NoListenPortConnectorTests.cs:42`

Found by Find-WeakTests.ps1 (weak-assertion) and confirmed by reading the test. Its only assertion is Assert.IsNotNull(result.Connection). The name promises the connect reaches the wrapped SwsHttpServerConnector, but a connector that returned any other connection for another port would also pass. The sibling MqttServerConnectorTests.OtherPorts_ReachTheWrappedServer at least adds Assert.IsNotInstanceOfType.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.UnitTests/NoListenPortConnectorTests.cs -Pattern "Assert\."
```

- Expected: An assertion that identifies the wrapped server's connection (its type, or bytes it serves).
- Actual: NoListenPortConnectorTests.cs:42:        Assert.IsNotNull(result.Connection);

## Re-audits

- 2026-10-13 | 2026-10-13_0925.md | reproduces: no | Ran the reproduction: the Assert lines of ConnectAsync_OtherPort_ReachesTheWrappedServer are now 'SwsHttpServerConnection connection = Assert.IsInstanceOfType<SwsHttpServerConnection>(result.Connection);', 'Assert.AreEqual(CurlExitCode.Ok, result.ExitCode);' and 'Assert.AreEqual(new IPEndPoint(IPAddress.Loopback, 8990), connection.RemoteEndPoint);', so the test now pins the wrapped server's connection.
- 2026-10-13 | 2026-10-13_1822.md | reproduces: no | Ran Select-String -Path Curl.Conformance.UnitTests/NoListenPortConnectorTests.cs -Pattern "Assert\.". ConnectAsync_OtherPort_ReachesTheWrappedServer (lines 43-45) now asserts Assert.IsInstanceOfType<SwsHttpServerConnection>(result.Connection), exit Ok, and connection.RemoteEndPoint == 127.0.0.1:8990, so it checks that the connection is the wrapped server's.

## Log

- 2026-10-12: filed proposed.
- 2026-10-12: proposed -> accepted.
- 2026-10-13: accepted -> closed. Re-audit 2026-10-13_1822.md: the reproduction no longer reproduces.
