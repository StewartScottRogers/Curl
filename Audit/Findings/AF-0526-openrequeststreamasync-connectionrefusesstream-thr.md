---
id: AF-0526
title: OpenRequestStreamAsync_ConnectionRefusesStream_ThrowsItsIOException checks only the exception type, not that it is the connection's own IOException
auditor: quality
severity: Low
status: proposed
reason:
key: quality:Curl.Http3.UnitTests/Http3ClientSessionTests.cs:OpenRequestStreamAsync_ConnectionRefusesStream_ThrowsItsIOException:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-02
found-at: 4698726450f0ce3447bd6790d0bf9c59787bae95
scorecard: 2026-11-02_0645.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0526 - OpenRequestStreamAsync_ConnectionRefusesStream_ThrowsItsIOException checks only the exception type, not that it is the connection's own IOException

## Summary

Low finding from the quality auditor at `Curl.Http3.UnitTests/Http3ClientSessionTests.cs:47`: OpenRequestStreamAsync_ConnectionRefusesStream_ThrowsItsIOException checks only the exception type, not that it is the connection's own IOException. Reported by an auditor flagged unreliable in 2026-11-02_0645.md.

## Evidence

Location: `Curl.Http3.UnitTests/Http3ClientSessionTests.cs:47`

Step-0 candidate (name-lies). Body: 'InMemoryMultiplexedConnection connection = new() { BidirectionalOpenFailure = new IOException("refused") }; ... await Assert.ThrowsExactlyAsync<IOException>(async () => await session.OpenRequestStreamAsync(null, CancellationToken.None));'. The name promises 'its' IOException, the one the connection raised, but the test checks neither the instance (Assert.AreSame) nor the message 'refused'. A session that swallowed the connection's failure and threw a new IOException of its own (another message, lost inner exception) would still pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Http3.UnitTests/Http3ClientSessionTests.cs -Pattern 'ThrowsExactlyAsync<IOException>' -Context 0,2
```

- Expected: The thrown exception is captured and compared with the connection's BidirectionalOpenFailure (Assert.AreSame) or its message 'refused'.
- Actual: Http3ClientSessionTests.cs:47: await Assert.ThrowsExactlyAsync<IOException>(...); followed by no further assertion.

## Re-audits

## Log

- 2026-11-02: filed proposed.
