---
id: BL-5679
title: Fix AF-0929: SignalRReferenceRequest.TryRead is a Try method that returns a byte count, not a bool
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceRequest.cs, Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceHub.cs, Curl.Conformance.Extended.UnitLibrary/SocketIo/SocketIoReferenceServer.cs, Curl.Conformance.Extended.UnitTests/SignalR/SignalRReferenceHubTests.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5679 — Fix AF-0929: SignalRReferenceRequest.TryRead is a Try method that returns a byte count, not a bool

## Goal

The defect the audit office reported as AF-0929 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0929 (Medium, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceRequest.cs:29`

Location: `Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceRequest.cs:29`

'public static int TryRead(ReadOnlySpan<byte> pending, out SignalRReferenceRequest? request)', documented '<returns>The number of bytes the request took; 0 when it is not whole yet.</returns>'. align-and-document's definition of an aligned name says 'A method named Try... returns bool'. This one returns the bytes consumed, so a caller reading the name would test it as a success flag (any non-zero count is truthy only by convention in C, not in C#). A name such as ReadWholeRequest or TakeRequest, returning the length, would say what it does.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceRequest.cs -Pattern 'public static \w+ TryRead'
```

- Expected: A method named TryRead returns bool, or the method has a name that says it returns a length.
- Actual: SignalRReferenceRequest.cs:29: public static int TryRead(ReadOnlySpan<byte> pending, out SignalRReferenceRequest? request)

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Renamed TryRead to TakeWholeRequest (returns the bytes taken, 0 when not whole). Callers in SignalRReferenceHub, SocketIoReferenceServer and the hub tests updated; added them to touches (no other Doing task names them). Build clean, 1737 tests pass, quality run 0 failing members.

## Log

- 2026-11-21: Created.
- 2026-11-21: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
