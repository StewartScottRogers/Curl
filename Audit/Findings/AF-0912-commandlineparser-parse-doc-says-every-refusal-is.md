---
id: AF-0912
title: CommandLineParser.Parse doc says every refusal is exit 2 except -d @file, but --proto-default refuses with 1 and --url @file and -K with 26
auditor: truthfulness
severity: High
status: closed
reason: Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces.
key: truthfulness:Curl.Cli.UnitLibrary/CommandLineParser.cs:CommandLineParser.Parse:false-doc-comment
reproduction: none
task: BL-5627
tasks: BL-5627
found: 2026-11-20
found-at: 1ce730765b795e99d4d034faf03432b41b5ec6a4
scorecard: 2026-11-20_0456.md
duplicate-of:
closed: 2026-11-20
closed-how: reliable-reaudit
closed-by: 2026-11-20_1740.md
---
# AF-0912 - CommandLineParser.Parse doc says every refusal is exit 2 except -d @file, but --proto-default refuses with 1 and --url @file and -K with 26

## Summary

High finding from the truthfulness auditor at `Curl.Cli.UnitLibrary/CommandLineParser.cs:73`: CommandLineParser.Parse doc says every refusal is exit 2 except -d @file, but --proto-default refuses with 1 and --url @file and -K with 26.

## Evidence

Location: `Curl.Cli.UnitLibrary/CommandLineParser.cs:73`

The <returns> of both Parse overloads (lines 64-73 and 75-91) says: 'every refusal carries CurlExitCode.FailedInit, except a -d @file that cannot be read, which carries CurlExitCode.ReadError.' Three other refusals have other codes. CommandLineOptionTable.cs:1403 returns CommandLineRefusal.UnsupportedProtocol(spelledOption) for an unknown --proto-default scheme, and CommandLineRefusal.cs:232 builds it with CurlExitCode.UnsupportedProtocol (exit 1). CommandLineOptionTable.cs:918 returns CommandLineRefusal.UrlFileUnreadable for an unreadable --url @file, exit 26 (CommandLineRefusal.cs:171, CurlExitCode.ReadError). ConfigFileApplier.cs:49 and :83 return CommandLineRefusal.ConfigFileUnreadable for an unreadable -K file, exit 26 (CurlExitCode.ReadError). A caller trusting the doc would map every non-ReadError refusal to exit 2 and every ReadError to -d @file.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitLibrary/CommandLineOptionTable.cs,Curl.Cli.UnitLibrary/ConfigFileApplier.cs,Curl.Cli.UnitLibrary/CommandLineRefusal.cs -Pattern 'CommandLineRefusal\.(UnsupportedProtocol|UrlFileUnreadable|ConfigFileUnreadable)\(|new CommandLineRefusal\(CurlExitCode\.(UnsupportedProtocol|ReadError)'
```

- Expected: Either no parser refusal other than -d @file carries an exit code other than FailedInit, or the Parse doc names the others (exit 1 for --proto-default, exit 26 for --url @file and -K).
- Actual: CommandLineOptionTable.cs:918 UrlFileUnreadable (exit 26), CommandLineOptionTable.cs:1403 UnsupportedProtocol (exit 1), ConfigFileApplier.cs:49/:83 ConfigFileUnreadable (exit 26), CommandLineRefusal.cs:171 and :232 building them with ReadError and UnsupportedProtocol; the Parse doc names only -d @file.

## Re-audits

- 2026-11-20 | 2026-11-20_1740.md | reproduces: no | Ran the reproduction. It still lists the non-2 refusal sites: CommandLineOptionTable.cs:918 UrlFileUnreadable, :1403 UnsupportedProtocol, ConfigFileApplier.cs:49/:83 ConfigFileUnreadable, CommandLineRefusal.cs:171/:470 ReadError, :232 UnsupportedProtocol. Those lines show the code, not the defect. The doc they were checked against is now right: CommandLineParser.cs:67-71 says every refusal carries FailedInit 'except a -d @file, a --url @file or a -K file that cannot be read, which carry ReadError, and a --proto-default naming an unknown scheme, which carries UnsupportedProtocol'. The ReadError and UnsupportedProtocol constructions in CommandLineRefusal.cs (lines 171, 232, 449, 470, 492, 773) all fall in those named cases.

## Log

- 2026-11-20: filed proposed.
- 2026-11-20: accepted -> closed. Re-audit 2026-11-20_1740.md: the reproduction no longer reproduces.
