---
id: AF-0688
title: SendAsync_PortClosed_ThrowsCurlsExit7Text never asserts exit 7
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces.
key: quality:Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:SendAsync_PortClosed_ThrowsCurlsExit7Text:name-lies
reproduction: none
task: BL-4882
tasks: BL-4882
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-13
closed-how: reliable-reaudit
closed-by: 2026-11-13_2321.md
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

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Ran the Select-String: the test is now SendAsync_PortClosed_ThrowsCouldntConnectText (line 50) and asserts the exit-7 text it names (Assert.EndsWith(CouldntConnectWords, refused.Message), line 67).
- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Select-String now matches SendAsync_PortClosed_ThrowsCouldntConnectText (line 50) with Assert.EndsWith(CouldntConnectWords, refused.Message) (line 67) and the exact message AreEqual; the exit 7 claim is gone from the name.

## Log

- 2026-11-05: filed proposed.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces.
