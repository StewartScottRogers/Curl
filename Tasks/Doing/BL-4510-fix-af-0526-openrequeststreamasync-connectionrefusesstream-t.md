---
id: BL-4510
title: Fix AF-0526: OpenRequestStreamAsync_ConnectionRefusesStream_ThrowsItsIOException checks only the exception type, not that it is the connection's own IOException
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Http3.UnitTests]
requirement: none
created: 2026-11-02
completed:
---
# BL-4510 — Fix AF-0526: OpenRequestStreamAsync_ConnectionRefusesStream_ThrowsItsIOException checks only the exception type, not that it is the connection's own IOException

## Goal

The defect the audit office reported as AF-0526 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0526 (Low, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0526-openrequeststreamasync-connectionrefusesstream-thr.md`.

Location: `Curl.Http3.UnitTests/Http3ClientSessionTests.cs:47`

Location: `Curl.Http3.UnitTests/Http3ClientSessionTests.cs:47`

Step-0 candidate (name-lies). Body: 'InMemoryMultiplexedConnection connection = new() { BidirectionalOpenFailure = new IOException("refused") }; ... await Assert.ThrowsExactlyAsync<IOException>(async () => await session.OpenRequestStreamAsync(null, CancellationToken.None));'. The name promises 'its' IOException, the one the connection raised, but the test checks neither the instance (Assert.AreSame) nor the message 'refused'. A session that swallowed the connection's failure and threw a new IOException of its own (another message, lost inner exception) would still pass.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Http3.UnitTests/Http3ClientSessionTests.cs -Pattern 'ThrowsExactlyAsync<IOException>' -Context 0,2
```

- Expected: The thrown exception is captured and compared with the connection's BidirectionalOpenFailure (Assert.AreSame) or its message 'refused'.
- Actual: Http3ClientSessionTests.cs:47: await Assert.ThrowsExactlyAsync<IOException>(...); followed by no further assertion.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-02: Created.
- 2026-11-02: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
