---
id: AF-0039
title: FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal asserts only IsNotNull on the result
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Networking.UnitTests/FastOpenSocketOptionTests.cs:FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal:weak-assertion
task: none
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
closed:
closed-by:
---
# AF-0039 - FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal asserts only IsNotNull on the result

## Summary

Low finding from the quality auditor at `Curl.Networking.UnitTests/FastOpenSocketOptionTests.cs:104`: FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal asserts only IsNotNull on the result. Reported by an auditor flagged unreliable in 2026-10-03_1459.md.

## Evidence

Location: `Curl.Networking.UnitTests/FastOpenSocketOptionTests.cs:104`

Line 104 `Assert.IsNotNull(new TcpDialer(...).FailureToOpenSocket(...))` is the only assertion. The name promises the system's refusal, but any non-null failure passes.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/FastOpenSocketOptionTests.cs -Pattern 'Assert\.IsNotNull\(new TcpDialer'
```

- Expected: No match: the test asserts the refusal itself.
- Actual: FastOpenSocketOptionTests.cs:104:        Assert.IsNotNull(new TcpDialer(new TcpSocketOptions(MultipathTcp: true)).FailureToOpenSocket(AddressFamily.InterNetwork));

## Re-audits

## Log

- 2026-10-03: filed proposed.
