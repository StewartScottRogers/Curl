---
id: AF-0946
title: SignalRReferenceFrame.TryRead is a Try method that returns a byte count, not a bool
auditor: truthfulness
severity: Medium
status: closed
reason: Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceFrame.cs:SignalRReferenceFrame.TryRead:misleading-name
reproduction: none
task: BL-5705
tasks: BL-5705
found: 2026-11-21
found-at: 57bece6c118995fac41401b81793a8395d3fb16d
scorecard: 2026-11-21_0835.md
duplicate-of:
closed: 2026-11-21
closed-how: reliable-reaudit
closed-by: 2026-11-21_1740.md
---
# AF-0946 - SignalRReferenceFrame.TryRead is a Try method that returns a byte count, not a bool

## Summary

Medium finding from the truthfulness auditor at `Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceFrame.cs:32`: SignalRReferenceFrame.TryRead is a Try method that returns a byte count, not a bool. Reported by an auditor flagged unreliable in 2026-11-21_0835.md.

## Evidence

Location: `Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceFrame.cs:32`

Line 32: public static int TryRead(ReadOnlySpan<byte> pending, out SignalRReferenceFrame? frame). Its doc says '<returns>The number of bytes the frame took; 0 when it is not whole yet.</returns>'. align-and-document's definition says 'A method named Try... returns bool and does not throw for the expected failure'. This method returns an int byte count, so a reader who takes the name at its word treats the result as success or failure and loses the length. This is the same fault as AF-0929: that finding's sibling SignalRReferenceRequest.TryRead was renamed TakeWholeRequest, but the frame reader beside it kept the Try name. SignalRReferenceParser.cs's doc cites it as the example ('such as SignalRReferenceFrame.TryRead'), and SignalRReferenceHub.cs:131 passes it as a SignalRReferenceParser<T> delegate, whose doc also returns a byte count.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceFrame.cs -Pattern 'public static \w+ TryRead'
```

- Expected: No match: the method is named for what it does (for example TakeWholeFrame, as SignalRReferenceRequest.TakeWholeRequest is), or a TryRead returns bool.
- Actual: SignalRReferenceFrame.cs:32:    public static int TryRead(ReadOnlySpan<byte> pending, out SignalRReferenceFrame? frame)

## Re-audits

- 2026-11-21 | 2026-11-21_1740.md | reproduces: no | Ran Select-String -Path Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceFrame.cs -Pattern 'public static \w+ TryRead': no match. The method is now 'public static int TakeWholeFrame(ReadOnlySpan<byte> pending, out SignalRReferenceFrame? frame)' (line 32).

## Log

- 2026-11-21: filed proposed.
- 2026-11-21: accepted -> closed. Re-audit 2026-11-21_1740.md: the reproduction no longer reproduces.
