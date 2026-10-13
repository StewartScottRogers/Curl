---
id: BL-2121
title: Fix AF-0175: ConnectAsync_OtherPort_ReachesTheWrappedServer asserts only that some connection exists, not that it is the wrapped server's
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Conformance.UnitTests]
requirement: none
created: 2026-10-13
completed:
---
# BL-2121 — Fix AF-0175: ConnectAsync_OtherPort_ReachesTheWrappedServer asserts only that some connection exists, not that it is the wrapped server's

## Goal

The defect the audit office reported as AF-0175 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0175 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0175-connectasync-otherport-reachesthewrappedserver-ass.md`.

Location: `Curl.Conformance.UnitTests/NoListenPortConnectorTests.cs:42`

Location: `Curl.Conformance.UnitTests/NoListenPortConnectorTests.cs:42`

Found by Find-WeakTests.ps1 (weak-assertion) and confirmed by reading the test. Its only assertion is Assert.IsNotNull(result.Connection). The name promises the connect reaches the wrapped SwsHttpServerConnector, but a connector that returned any other connection for another port would also pass. The sibling MqttServerConnectorTests.OtherPorts_ReachTheWrappedServer at least adds Assert.IsNotInstanceOfType.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.UnitTests/NoListenPortConnectorTests.cs -Pattern "Assert\."
```

- Expected: An assertion that identifies the wrapped server's connection (its type, or bytes it serves).
- Actual: NoListenPortConnectorTests.cs:42:        Assert.IsNotNull(result.Connection);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-13: Created.
- 2026-10-12: Backlog -> Doing.
