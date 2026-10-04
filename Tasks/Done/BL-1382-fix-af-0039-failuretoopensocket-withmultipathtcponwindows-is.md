---
id: BL-1382
title: Fix AF-0039: FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal asserts only IsNotNull on the result
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitTests]
requirement: none
created: 2026-10-03
completed: 2026-10-03
---
# BL-1382 — Fix AF-0039: FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal asserts only IsNotNull on the result

## Goal

The defect the audit office reported as AF-0039 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0039 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0039-failuretoopensocket-withmultipathtcponwindows-isth.md`.

Location: `Curl.Networking.UnitTests/FastOpenSocketOptionTests.cs:104`

Location: `Curl.Networking.UnitTests/FastOpenSocketOptionTests.cs:104`

Line 104 `Assert.IsNotNull(new TcpDialer(...).FailureToOpenSocket(...))` is the only assertion. The name promises the system's refusal, but any non-null failure passes.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/FastOpenSocketOptionTests.cs -Pattern 'Assert\.IsNotNull\(new TcpDialer'
```

- Expected: No match: the test asserts the refusal itself.
- Actual: FastOpenSocketOptionTests.cs:104:        Assert.IsNotNull(new TcpDialer(new TcpSocketOptions(MultipathTcp: true)).FailureToOpenSocket(AddressFamily.InterNetwork));

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The test now asserts `SocketError.ProtocolNotSupported` (WSAEPROTONOSUPPORT, the "Protocol not supported" curl 8.21.0 prints on Windows per BL-647) as the refusal's `SocketErrorCode`. The code, not the message, is pinned because `SocketException.Message` is localised. No production change.

## Log

- 2026-10-03: Created.
- 2026-10-03: Backlog -> Doing.
- 2026-10-03: Doing -> Done. MPTCP-on-Windows test asserts SocketError.ProtocolNotSupported, not just non-null (AF-0039)
