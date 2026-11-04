---
id: AF-0552
title: BuildsTheDispatcherOverASocketCallbackPool checks the dispatcher only for null
auditor: quality
severity: Low
status: closed
reason: Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
key: quality:Curl.Engine.UnitTests/ManagedEasyTransferTests.Sockets.cs:CreateDispatcher_SocketCallbackSet_BuildsTheDispatcherOverASocketCallbackPool:weak-assertion
reproduction: none
task: BL-4700
tasks: BL-4700
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: consecutive
closed-by: 2026-11-03_1017.md, 2026-11-03_1615.md
---
# AF-0552 - BuildsTheDispatcherOverASocketCallbackPool checks the dispatcher only for null

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/ManagedEasyTransferTests.Sockets.cs:71`: BuildsTheDispatcherOverASocketCallbackPool checks the dispatcher only for null. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.Sockets.cs:71`

'Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(options, shared, cookieStore: null));' is the only check of the dispatcher; the other assertions check CreateConnectOnlyConnector. A dispatcher built without the SocketCallbackConnector passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedEasyTransferTests.Sockets.cs -Pattern 'Assert.IsNotNull\(ManagedCurlEngine.CreateDispatcher'
```

- Expected: An assertion that the dispatcher's pool sits over a SocketCallbackConnector.
- Actual: ManagedEasyTransferTests.Sockets.cs:71: Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher(...));

## Re-audits

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | No match for Assert.IsNotNull(ManagedCurlEngine.CreateDispatcher; the test performs through the dispatcher and asserts CouldntConnect, the error text and the open-socket callback call.
- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | No match; the test now performs through the dispatcher and asserts CurlCode.CouldntConnect, the SocketCallbackConnector text and the 'open IpConnection' callback.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: a second consecutive re-audit by its own auditor found the reproduction no longer reproduces (2026-11-03_1017.md, 2026-11-03_1615.md).
