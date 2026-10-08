---
id: AF-0039
title: FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal asserts only IsNotNull on the result
auditor: quality
severity: Low
status: accepted
reason: 
key: quality:Curl.Networking.UnitTests/FastOpenSocketOptionTests.cs:FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal:weak-assertion
reproduction: none
task: BL-1382
tasks: BL-1382
found: 2026-10-03
found-at: 2c24c2d74dc2c9775b64948efc3ca57b8937627e
scorecard: 2026-10-03_1459.md
duplicate-of:
closed:
closed-how:
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

- 2026-10-07 | 2026-10-07_0844.md | reproduces: no | Ran the reproduction: no 'Assert.IsNotNull(new TcpDialer' match in FastOpenSocketOptionTests.cs. FailureToOpenSocket_WithMultipathTcpOnWindows_IsTheSystemsRefusal now asserts Assert.AreEqual(SocketError.ProtocolNotSupported, refusal?.SocketErrorCode).
- 2026-10-07 | 2026-10-07_1336.md | not re-audited | overlaps planted defect PD-101 in Curl.Cryptography.UnitLibrary/AeadChaCha20Poly1305.cs, so the auditor's verdict (reproduces no) is set aside: Ran the Select-String reproduction: no match. The test now asserts Assert.AreEqual(SocketError.ProtocolNotSupported, refusal?.SocketErrorCode).

## Log

- 2026-10-03: filed proposed.
- 2026-10-03: proposed -> accepted.
