---
id: BL-3361
title: Fix AF-0384: CommandLineParser's summary says its one file-system question is the --cacert path and that only --variable %name reads the environment; six options probe paths and every off-Windows parse reads CA environment variables
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Cli.UnitLibrary]
requirement: none
created: 2026-10-23
completed:
---
# BL-3361 — Fix AF-0384: CommandLineParser's summary says its one file-system question is the --cacert path and that only --variable %name reads the environment; six options probe paths and every off-Windows parse reads CA environment variables

## Goal

The defect the audit office reported as AF-0384 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0384 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0384-commandlineparser-s-summary-says-its-one-file-syst.md`.

Location: `Curl.Cli.UnitLibrary/CommandLineParser.cs:17`

Location: `Curl.Cli.UnitLibrary/CommandLineParser.cs:17`

Summary, lines 17-18: 'The one file-system question it asks is whether the --cacert path exists, through a check Parse(...) takes as a parameter.' Six options use SettingExistingFile, which calls pathExists (CommandLineOptionTable.cs:127 --netrc-file, 216 --cacert, 218 --crlfile, 225 --proxy-cacert, 234 --proxy-crlfile, 248 --knownhosts). ConfigFileApplier.ApplyDefaultFile (called at CommandLineParser.cs:223) and VariableDefinition.cs:134 also call pathExists. Lines 31-33: the overloads without a DefaultConfigFileSearch 'never do, so what they return does not depend on the machine they run on, except that a --variable %name reads this process's environment'. But ParseArguments calls CaCertificateEnvironment.Apply(group) for every group (CommandLineParser.cs ~251). Off Windows (isWindows false), that reads CURL_CA_BUNDLE, SSL_CERT_DIR and SSL_CERT_FILE through group.ReadEnvironmentVariable (CaCertificateEnvironment.cs:35). The overloads without a reader use ProcessEnvironmentVariable = Environment.GetEnvironmentVariable. Refusal texts also read COLUMNS (CommandLineOptionTable.cs:1064, 1158; ConfigFileApplier.cs:49). So results depend on the machine's environment beyond --variable %name.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Cli.UnitLibrary/CommandLineParser.cs, Curl.Cli.UnitLibrary/CommandLineOptionTable.cs, Curl.Cli.UnitLibrary/CaCertificateEnvironment.cs -Pattern 'The one file-system question it asks', 'except that a\s*$', 'SettingExistingFile\("--', 'group\.ReadEnvironmentVariable\(name\)' | ForEach-Object { '{0}:{1}' -f $_.Filename, $_.LineNumber }
```

- Expected: Either the doc claims match (one SettingExistingFile row, no CA environment read), or the doc lists every path check and environment read.
- Actual: CommandLineParser.cs:17, CommandLineParser.cs:32, CommandLineOptionTable.cs:127, 216, 218, 225, 234, 248, CaCertificateEnvironment.cs:35: six path-checked options and an environment read the doc denies.

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
- 2026-10-23: Backlog -> Doing. Criteria at claim: b49d23ab 9742157d.
