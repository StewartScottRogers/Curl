---
id: AF-0687
title: ReceiveAsync_PortClosed_ThrowsCurlsExit7TextWithTheElapsedTime never asserts exit 7
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:ReceiveAsync_PortClosed_ThrowsCurlsExit7TextWithTheElapsedTime:name-lies
reproduction: none
task: none
tasks:
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0687 - ReceiveAsync_PortClosed_ThrowsCurlsExit7TextWithTheElapsedTime never asserts exit 7

## Summary

High finding from the quality auditor at `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:22`: ReceiveAsync_PortClosed_ThrowsCurlsExit7TextWithTheElapsedTime never asserts exit 7.

## Evidence

Location: `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:22`

The name states exit 7. The body asserts only the exception type, Assert.AreEqual("Failed to connect to localhost:1 after 5 ms: Could not connect to server", refused.Message), and the SocketErrorCode. Nothing checks 7 or CurlExitCode.CouldntConnect, so the exception could map to any exit code. Find-WeakTests flagged it as name-lies.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs -Pattern 'CouldntConnect|ExitCode'
```

- Expected: A match: an assertion of CurlExitCode.CouldntConnect (7).
- Actual: No match.

## Re-audits

## Log

- 2026-11-05: filed proposed.
