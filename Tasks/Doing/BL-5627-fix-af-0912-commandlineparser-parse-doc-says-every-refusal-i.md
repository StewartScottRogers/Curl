---
id: BL-5627
title: Fix AF-0912: CommandLineParser.Parse doc says every refusal is exit 2 except -d @file, but --proto-default refuses with 1 and --url @file and -K with 26
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Cli.UnitLibrary/CommandLineParser.cs]
requirement: none
created: 2026-11-20
completed:
---
# BL-5627 — Fix AF-0912: CommandLineParser.Parse doc says every refusal is exit 2 except -d @file, but --proto-default refuses with 1 and --url @file and -K with 26

## Goal

The defect the audit office reported as AF-0912 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0912 (High, truthfulness auditor), which Stewart accepted. The finding's file is on the audit office's paths, which a dark factory lane may not read or run; everything this task needs is below.

Location: `Curl.Cli.UnitLibrary/CommandLineParser.cs:73`

Location: `Curl.Cli.UnitLibrary/CommandLineParser.cs:73`

The <returns> of both Parse overloads (lines 64-73 and 75-91) says: 'every refusal carries CurlExitCode.FailedInit, except a -d @file that cannot be read, which carries CurlExitCode.ReadError.' Three other refusals have other codes. CommandLineOptionTable.cs:1403 returns CommandLineRefusal.UnsupportedProtocol(spelledOption) for an unknown --proto-default scheme, and CommandLineRefusal.cs:232 builds it with CurlExitCode.UnsupportedProtocol (exit 1). CommandLineOptionTable.cs:918 returns CommandLineRefusal.UrlFileUnreadable for an unreadable --url @file, exit 26 (CommandLineRefusal.cs:171, CurlExitCode.ReadError). ConfigFileApplier.cs:49 and :83 return CommandLineRefusal.ConfigFileUnreadable for an unreadable -K file, exit 26 (CurlExitCode.ReadError). A caller trusting the doc would map every non-ReadError refusal to exit 2 and every ReadError to -d @file.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitLibrary/CommandLineOptionTable.cs,Curl.Cli.UnitLibrary/ConfigFileApplier.cs,Curl.Cli.UnitLibrary/CommandLineRefusal.cs -Pattern 'CommandLineRefusal\.(UnsupportedProtocol|UrlFileUnreadable|ConfigFileUnreadable)\(|new CommandLineRefusal\(CurlExitCode\.(UnsupportedProtocol|ReadError)'
```

- Expected: Either no parser refusal other than -d @file carries an exit code other than FailedInit, or the Parse doc names the others (exit 1 for --proto-default, exit 26 for --url @file and -K).
- Actual: CommandLineOptionTable.cs:918 UrlFileUnreadable (exit 26), CommandLineOptionTable.cs:1403 UnsupportedProtocol (exit 1), ConfigFileApplier.cs:49/:83 ConfigFileUnreadable (exit 26), CommandLineRefusal.cs:171 and :232 building them with ReadError and UnsupportedProtocol; the Parse doc names only -d @file.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-20: Created.
- 2026-11-20: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
