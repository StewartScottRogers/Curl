---
id: BL-5705
title: Fix AF-0946: SignalRReferenceFrame.TryRead is a Try method that returns a byte count, not a bool
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceFrame.cs]
requirement: none
created: 2026-11-21
completed:
---
# BL-5705 — Fix AF-0946: SignalRReferenceFrame.TryRead is a Try method that returns a byte count, not a bool

## Goal

The defect the audit office reported as AF-0946 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0946 (Medium, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceFrame.cs:32`

Location: `Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceFrame.cs:32`

Line 32: public static int TryRead(ReadOnlySpan<byte> pending, out SignalRReferenceFrame? frame). Its doc says '<returns>The number of bytes the frame took; 0 when it is not whole yet.</returns>'. align-and-document's definition says 'A method named Try... returns bool and does not throw for the expected failure'. This method returns an int byte count, so a reader who takes the name at its word treats the result as success or failure and loses the length. This is the same fault as AF-0929: that finding's sibling SignalRReferenceRequest.TryRead was renamed TakeWholeRequest, but the frame reader beside it kept the Try name. SignalRReferenceParser.cs's doc cites it as the example ('such as SignalRReferenceFrame.TryRead'), and SignalRReferenceHub.cs:131 passes it as a SignalRReferenceParser<T> delegate, whose doc also returns a byte count.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceFrame.cs -Pattern 'public static \w+ TryRead'
```

- Expected: No match: the method is named for what it does (for example TakeWholeFrame, as SignalRReferenceRequest.TakeWholeRequest is), or a TryRead returns bool.
- Actual: SignalRReferenceFrame.cs:32:    public static int TryRead(ReadOnlySpan<byte> pending, out SignalRReferenceFrame? frame)

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-21: Created.
