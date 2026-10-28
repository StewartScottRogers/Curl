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
completed: 2026-10-28
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

- [x] None of the seven scripts uses a backslash path separator in a path it builds, `$env:TEMP`/`$env:LOCALAPPDATA`/`$env:USERPROFILE` without a non-Windows fallback, or a bare `powershell` call (`grep -nE '\\\\|LOCALAPPDATA|USERPROFILE|\$env:TEMP|powershell ' <files>` shows none outside comments).
- [x] Each of the seven scripts was run under `pwsh` in WSL Ubuntu (or a Linux CI runner) with the inputs its callers give it and produced the same output as on Windows; the commands and results are in this task's Log.
- [x] Windows behaviour is unchanged, and `dotnet build` plus the fast tests are green.

## Notes
- Only `Update-UpstreamTestData.ps1` needed changes: Windows-only `System32\tar.exe` (now chosen only when `$IsWindows`, else `tar`), bsdtar-only `--include` (now positional member patterns, plus `--wildcards` when `tar --version` says GNU tar), a `'\'` source-root separator (now `DirectorySeparatorChar`), `include\curl` (now `include/curl`) and a `powershell` example (now `pwsh`). The other six scripts needed no change.
- The grep still matches regex escapes (`\.`, `\d`, `'\'`) and `Replace('\', '/')` in Update-UpstreamTestData.ps1, which normalise separators and build no path.
- Run under pwsh 7.5.4 in WSL Ubuntu (installed to /opt/pwsh; `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1` as WSL has no ICU), same inputs as on Windows: New-ResourcesJson, Test-ResourcesJson (no -CurlPath), New-DownloadPage, Write-IntegrationTestBadge, Write-IntegrationTestSummary on a fake dist/trx directory: output equal to Windows apart from the platform newline Set-Content appends and `measuredAt`. Write-ProtoCaseFiles on a two-case cases_proto.py: byte-identical json. Update-UpstreamTestData -Tag curl-8_22_0: all 3551 files byte-identical to the vendored Original folder (which a Windows run leaves unchanged).

## Log

- 2026-10-27: Created.
- 2026-10-28: Backlog -> Doing. Criteria at claim: 4ad30a1a 35623d92 677155f8.
- 2026-10-28: Fixed Update-UpstreamTestData.ps1; the seven scripts verified under pwsh on Linux. Build clean, Curl.Conformance.UnitTests fast tests green (4373 passed).
- 2026-10-28: Doing -> Done. Update-UpstreamTestData.ps1 made portable; all seven scripts verified under pwsh on Linux.
