---
id: AF-0296
title: TransferSettingsOf's ShowError: false can become true with no test failing, so a libcurl-API SSH transfer writes the tool's known_hosts error line
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-22_0130.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:TransferSettingsOf-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1906:false
task: BL-2934
tasks: BL-2934
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed: 2026-10-22
closed-how: mechanical
closed-by: 2026-10-22_0130.md
---
# AF-0296 - TransferSettingsOf's ShowError: false can become true with no test failing, so a libcurl-API SSH transfer writes the tool's known_hosts error line

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1906`: TransferSettingsOf's ShowError: false can become true with no test failing, so a libcurl-API SSH transfer writes the tool's known_hosts error line.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1906`

Sampled mutant (seed 0) survived: ShowError: false, => ShowError: true, in the settings an easy-interface transfer gets when no ToolSettings are set (ManagedEasyTransfer.cs:589). TransferPerformer.Setup.cs:431 hides KnownHostsFileMissingLine ("curl: Could not find a known_hosts file") only when settings.Silent && !settings.ShowError. With the mutant, every libcurl-API SFTP/SCP transfer without a known_hosts file writes that curl-tool line to standard error, which libcurl never does. No Engine test pins TransferSettingsOf's ShowError or the easy path's silence.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1906:false -Member TransferSettingsOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: no | Ran the -Site reproduction: the site moved; resolved within TransferSettingsOf to line 1921, mutant Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1921:false killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-19: filed proposed.
- 2026-10-21: proposed -> accepted.
- 2026-10-22: accepted -> closed. Re-audit 2026-10-22_0130.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
