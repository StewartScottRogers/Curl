---
id: BL-2934
title: Fix AF-0296: TransferSettingsOf's ShowError: false can become true with no test failing, so a libcurl-API SSH transfer writes the tool's known_hosts error line
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-21
completed:
---
# BL-2934 — Fix AF-0296: TransferSettingsOf's ShowError: false can become true with no test failing, so a libcurl-API SSH transfer writes the tool's known_hosts error line

## Goal

The defect the audit office reported as AF-0296 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0296 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0296-transfersettingsof-s-showerror-false-can-become-tr.md`.

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1906`

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1906`

Sampled mutant (seed 0) survived: ShowError: false, => ShowError: true, in the settings an easy-interface transfer gets when no ToolSettings are set (ManagedEasyTransfer.cs:589). TransferPerformer.Setup.cs:431 hides KnownHostsFileMissingLine ("curl: Could not find a known_hosts file") only when settings.Silent && !settings.ShowError. With the mutant, every libcurl-API SFTP/SCP transfer without a known_hosts file writes that curl-tool line to standard error, which libcurl never does. No Engine test pins TransferSettingsOf's ShowError or the easy path's silence.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.cs:1906:false -Member TransferSettingsOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-21: Created.
