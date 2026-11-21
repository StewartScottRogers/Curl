---
id: AF-0929
title: SignalRReferenceRequest.TryRead is a Try method that returns a byte count, not a bool
auditor: truthfulness
severity: Medium
status: proposed
reason:
key: truthfulness:Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceRequest.cs:TryRead:misleading-name
reproduction: none
task: none
tasks:
found: 2026-11-21
found-at: 1f330a1f28a1086717db7833947030e8fdc0bf9d
scorecard: 2026-11-21_0114.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0929 - SignalRReferenceRequest.TryRead is a Try method that returns a byte count, not a bool

## Summary

Medium finding from the truthfulness auditor at `Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceRequest.cs:29`: SignalRReferenceRequest.TryRead is a Try method that returns a byte count, not a bool. Reported by an auditor flagged unreliable in 2026-11-21_0114.md.

## Evidence

Location: `Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceRequest.cs:29`

'public static int TryRead(ReadOnlySpan<byte> pending, out SignalRReferenceRequest? request)', documented '<returns>The number of bytes the request took; 0 when it is not whole yet.</returns>'. align-and-document's definition of an aligned name says 'A method named Try... returns bool'. This one returns the bytes consumed, so a caller reading the name would test it as a success flag (any non-zero count is truthy only by convention in C, not in C#). A name such as ReadWholeRequest or TakeRequest, returning the length, would say what it does.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Conformance.Extended.UnitLibrary/SignalR/SignalRReferenceRequest.cs -Pattern 'public static \w+ TryRead'
```

- Expected: A method named TryRead returns bool, or the method has a name that says it returns a length.
- Actual: SignalRReferenceRequest.cs:29: public static int TryRead(ReadOnlySpan<byte> pending, out SignalRReferenceRequest? request)

## Re-audits

## Log

- 2026-11-21: filed proposed.
