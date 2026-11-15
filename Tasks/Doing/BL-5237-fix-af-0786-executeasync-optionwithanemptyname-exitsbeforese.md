---
id: BL-5237
title: Fix AF-0786: ExecuteAsync_OptionWithAnEmptyName_ExitsBeforeSendingAByte checks only that the exit code is not Ok
priority: Low
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Original.Protocol.Telnet.UnitTests]
requirement: none
created: 2026-11-15
completed:
---
# BL-5237 — Fix AF-0786: ExecuteAsync_OptionWithAnEmptyName_ExitsBeforeSendingAByte checks only that the exit code is not Ok

## Goal

The defect the audit office reported as AF-0786 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0786 (Low, quality auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerAdversarialTests.cs:184`

Location: `Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerAdversarialTests.cs:184`

Step 1. The exit code's only check is Assert.AreNotEqual(CurlExitCode.Ok, exchange.Result.ExitCode); for the '', '=' and '==' -t options. Its neighbours pin exit 48 or 49 exactly, so a wrong non-zero exit code here would pass. Same shape as AF-0763.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Original.Protocol.Telnet.UnitTests/TelnetProtocolHandlerAdversarialTests.cs -Pattern 'Assert.AreNotEqual\(CurlExitCode.Ok, exchange.Result.ExitCode\)'
```

- Expected: No match: the exact exit code is asserted.
- Actual: TelnetProtocolHandlerAdversarialTests.cs:189: Assert.AreNotEqual(CurlExitCode.Ok, exchange.Result.ExitCode);

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

The three `-t` rows now pin their exit codes: "" gives 49 (no equals sign, SetoptOptionSyntax), "=" and "==" give 48 (UnknownOption). The reproduction pattern no longer matches. Fast tests pass here; the change is platform-neutral.

## Log

- 2026-11-15: Created.
- 2026-11-15: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
