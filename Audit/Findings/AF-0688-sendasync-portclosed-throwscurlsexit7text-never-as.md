---
id: AF-0688
title: SendAsync_PortClosed_ThrowsCurlsExit7Text never asserts exit 7
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:SendAsync_PortClosed_ThrowsCurlsExit7Text:name-lies
reproduction: none
task: BL-4882
tasks: BL-4882
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0688 - SendAsync_PortClosed_ThrowsCurlsExit7Text never asserts exit 7

## Summary

High finding from the quality auditor at `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:45`: SendAsync_PortClosed_ThrowsCurlsExit7Text never asserts exit 7. Reappeared; previously AF-0615.

## Evidence

Location: `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:45`

The name states exit 7. The body asserts only Assert.AreEqual("Failed to connect to localhost:1 after 7 ms: Could not connect to server", refused.Message) and the SocketErrorCode. No assertion of 7 or CurlExitCode.CouldntConnect.

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
