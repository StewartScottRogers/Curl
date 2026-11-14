---
id: AF-0687
title: ReceiveAsync_PortClosed_ThrowsCurlsExit7TextWithTheElapsedTime never asserts exit 7
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces.
key: quality:Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:ReceiveAsync_PortClosed_ThrowsCurlsExit7TextWithTheElapsedTime:name-lies
reproduction: none
task: BL-4881
tasks: BL-4881
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-13
closed-how: reliable-reaudit
closed-by: 2026-11-13_2321.md
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

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Ran the Select-String: the test is now ReceiveAsync_PortClosed_ThrowsCouldntConnectTextWithTheElapsedTime (line 26). It no longer names exit 7 and asserts the text it names (Assert.AreEqual("Failed to connect to localhost:1 after 5 ms: Could not connect to server", ...), Assert.EndsWith(CouldntConnectWords, ...)).
- 2026-11-13 | 2026-11-13_2321.md | reproduces: no | Select-String now matches the renamed ReceiveAsync_PortClosed_ThrowsCouldntConnectTextWithTheElapsedTime (line 26) and Assert.EndsWith(CouldntConnectWords, refused.Message) (line 43); the name no longer promises exit 7, and the text it does promise is asserted exactly.

## Log

- 2026-11-05: filed proposed.
- 2026-11-13: accepted -> closed. Re-audit 2026-11-13_2321.md: the reproduction no longer reproduces.
