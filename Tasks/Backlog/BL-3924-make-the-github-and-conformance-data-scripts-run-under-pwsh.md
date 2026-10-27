---
id: BL-3924
title: Make the .github and conformance data scripts run under pwsh on Linux and macOS
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [.github/integration, .github/release, .github/site, Curl.Conformance.Differential.UnitLibrary/Cases/CliProtocols/Proto/Write-ProtoCaseFiles.ps1, Curl.Conformance.UnitTests/CurlTestCases/Original/Update-UpstreamTestData.ps1]
requirement: none
created: 2026-10-27
completed:
---
# BL-3924 — Make the .github and conformance data scripts run under pwsh on Linux and macOS

## Goal

The seven PowerShell scripts under `.github/integration`, `.github/release`, `.github/site` and the
two conformance data scripts run unchanged in behaviour under `pwsh` on Linux and macOS.

## Context

Part of BL-3934 (the whole repository workable on Linux and macOS; Stewart chose 2026-10-27 to
keep one PowerShell copy that runs everywhere rather than bash rewrites). Files:
`.github/integration/Write-IntegrationTestBadge.ps1`, `.github/integration/Write-IntegrationTestSummary.ps1`,
`.github/release/New-ResourcesJson.ps1`, `.github/release/Test-ResourcesJson.ps1`,
`.github/site/New-DownloadPage.ps1`,
`Curl.Conformance.Differential.UnitLibrary/Cases/CliProtocols/Proto/Write-ProtoCaseFiles.ps1` and
`Curl.Conformance.UnitTests/CurlTestCases/Original/Update-UpstreamTestData.ps1` (the only one with a
Windows-only construct found by grep; check all seven for backslash paths, `$env:TEMP`,
`$env:LOCALAPPDATA`, `powershell` calls and case-sensitive file names). These are maintainer tools,
so they get no `.sh` launcher; `pwsh -File <script>` is the way to run them.

## Acceptance criteria

- [ ] None of the seven scripts uses a backslash path separator in a path it builds, `$env:TEMP`/`$env:LOCALAPPDATA`/`$env:USERPROFILE` without a non-Windows fallback, or a bare `powershell` call (`grep -nE '\\\\|LOCALAPPDATA|USERPROFILE|\$env:TEMP|powershell ' <files>` shows none outside comments).
- [ ] Each of the seven scripts was run under `pwsh` in WSL Ubuntu (or a Linux CI runner) with the inputs its callers give it and produced the same output as on Windows; the commands and results are in this task's Log.
- [ ] Windows behaviour is unchanged, and `dotnet build` plus the fast tests are green.

## Notes

## Log

- 2026-10-27: Created.
