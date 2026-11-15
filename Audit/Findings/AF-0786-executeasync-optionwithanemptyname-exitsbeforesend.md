---
id: AF-0786
title: ExecuteAsync_OptionWithAnEmptyName_ExitsBeforeSendingAByte checks only that the exit code is not Ok
auditor: quality
severity: Low
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerAdversarialTests.cs:ExecuteAsync_OptionWithAnEmptyName_ExitsBeforeSendingAByte:weak-assertion
reproduction: none
task: none
tasks:
found: 2026-11-15
found-at: d04b275ae7a1546a4d0feed894da1ba520945cf5
scorecard: 2026-11-15_0222.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0786 - ExecuteAsync_OptionWithAnEmptyName_ExitsBeforeSendingAByte checks only that the exit code is not Ok

## Summary

Low finding from the quality auditor at `Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerAdversarialTests.cs:184`: ExecuteAsync_OptionWithAnEmptyName_ExitsBeforeSendingAByte checks only that the exit code is not Ok.

## Evidence

Location: `Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerAdversarialTests.cs:184`

Step 1. The exit code's only check is Assert.AreNotEqual(CurlExitCode.Ok, exchange.Result.ExitCode); for the '', '=' and '==' -t options. Its neighbours pin exit 48 or 49 exactly, so a wrong non-zero exit code here would pass. Same shape as AF-0763.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerAdversarialTests.cs -Pattern 'Assert.AreNotEqual\(CurlExitCode.Ok, exchange.Result.ExitCode\)'
```

- Expected: No match: the exact exit code is asserted.
- Actual: TelnetProtocolHandlerAdversarialTests.cs:189: Assert.AreNotEqual(CurlExitCode.Ok, exchange.Result.ExitCode);

## Re-audits

## Log

- 2026-11-15: filed proposed.
