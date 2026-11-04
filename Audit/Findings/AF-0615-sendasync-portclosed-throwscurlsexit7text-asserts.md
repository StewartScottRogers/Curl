---
id: AF-0615
title: SendAsync_PortClosed_ThrowsCurlsExit7Text asserts only the text its own fake supplies, never exit 7 or curl's exit-7 text
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:SendAsync_PortClosed_ThrowsCurlsExit7Text:name-lies
reproduction: none
task: BL-4788
tasks: BL-4788
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0615 - SendAsync_PortClosed_ThrowsCurlsExit7Text asserts only the text its own fake supplies, never exit 7 or curl's exit-7 text

## Summary

High finding from the quality auditor at `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:43`: SendAsync_PortClosed_ThrowsCurlsExit7Text asserts only the text its own fake supplies, never exit 7 or curl's exit-7 text. Reported by an auditor flagged unreliable in 2026-11-03_1615.md.

## Evidence

Location: `Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs:43`

The test builds new PortUnreachableReportingDatagramChannel(new FailingChannel(...), () => "refused text") and its only assertion is Assert.AreEqual("refused text", refused.Message): the value its own fake returns. Neither 7, CurlExitCode.CouldntConnect nor curl's 'Failed to connect ... Could not connect to server' text appears, so the test cannot fail if a send to a closed port reports anything else. Its sibling ReceiveAsync_PortClosed_ThrowsCurlsExit7TextWithTheElapsedTime does assert curl's text.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Networking.UnitTests/PortUnreachableReportingDatagramChannelTests.cs -Pattern 'SendAsync_PortClosed_ThrowsCurlsExit7Text' -Context 0,14
```

- Expected: The body asserts curl's exit-7 text (or exit 7), not only the fake's own string.
- Actual: Only Assert.AreEqual("refused text", refused.Message); with () => "refused text" set up in the same test.

## Re-audits

- 2026-11-03 | 2026-11-03_2203.md | reproduces: no | Ran the reproduction and read on: line 61 asserts the exact message 'Failed to connect to localhost:1 after 7 ms: Could not connect to server'. That is curl's exit-7 text, built by the product from the ManualTimeProvider's 7 ms, not text the fake supplies (the fake throws a bare SocketException).

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
