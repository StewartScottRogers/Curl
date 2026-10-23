---
id: BL-3405
title: Add Set-DifferentialEnvironment.ps1: locate the reference curl 8.22.0 and libcurl and set the CURL_DIFFERENTIAL_* variables for local runs
priority: Normal
assignee: Claude
pipeline: direct
depends-on: [BL-3404]
touches: [Set-DifferentialEnvironment.ps1, Documentation/Wiki/Differential-Testing.md]
requirement: none
created: 2026-10-23
completed:
---
# BL-3405 — Add Set-DifferentialEnvironment.ps1: locate the reference curl 8.22.0 and libcurl and set the CURL_DIFFERENTIAL_* variables for local runs

## Goal

`Set-DifferentialEnvironment.ps1` at the repository root finds the reference curl 8.22.0 and its libcurl on this machine and sets `CURL_DIFFERENTIAL_REFERENCE`, `CURL_DIFFERENTIAL_REFERENCE_LIBRARY`, `CURL_DIFFERENTIAL_REFERENCE_LIBCURL` and `CURL_DIFFERENTIAL_PKI` for the current session, and a wiki page documents running the differential tests locally.

## Context

Stewart (2026-10-23) wants the 11 differential tests in `Curl.Conformance.Differential.IntegrationTests` to run instead of being skipped (they are Inconclusive while those variables are unset). No script fetches reference binaries: `Gap/Tools/Get-UpstreamRelease.ps1` downloads only the source tarball, and the gap office builds curl itself (read, do not change: `Gap/` is an audit path). On this machine the reference exists already: `/ucrt64/bin/curl.exe` is curl 8.22.0 (x86_64-w64-mingw32, Schannel, ADR-0018) beside `libcurl-4.dll` (Schannel) and `libcurl-openssl-4.dll`. Read the tests for which library each variable takes (`LibcurlEasyDifferentialTests` and its siblings use `CURL_DIFFERENTIAL_REFERENCE_LIBRARY`, `LibcurlScenarioDifferentialTests` uses `CURL_DIFFERENTIAL_REFERENCE_LIBCURL`; ADR-0782, ADR-0791, ADR-0792). The script: accepts `-ReferenceCurl` and `-ReferenceLibrary` to override; otherwise looks for a `curl.exe` that reports `curl 8.22.0` and `Schannel` in `curl -V` (PATH entries first, then the MSYS2 `ucrt64\bin` and `mingw64\bin` folders found from where `bash` or `pacman` lives), takes `libcurl-4.dll` from the same folder, calls `New-DifferentialPki.ps1` (BL-3404) into `%LOCALAPPDATA%\Curl\differential-pki` when no `-Pki` is given, sets the four variables with `$env:` in the calling session (so it is dot-sourced or its output is a block to run), and when no reference is found stops with a message naming what it looked for and how to point it elsewhere. No prompt, no download, BCL and PowerShell only. Document it in a new `Documentation/Wiki/Differential-Testing.md`: what the differential tests are, the four variables, running the script, the fast-test command that skips them and the command that runs them.

## Acceptance criteria

- [ ] On this machine, `. .\Set-DifferentialEnvironment.ps1` sets all four variables to paths that exist and `curl -V` of the first reports 8.22.0; with no 8.22.0 curl found it exits non-zero with the message above (shown by running it with `-ReferenceCurl` pointing at a nonexistent file).
- [ ] After running it, `dotnet test Curl.Conformance.Differential.IntegrationTests --filter "TestCategory=Integration"` reports no test Inconclusive because a variable is unset; state in Notes the count run and any that still skip, with the reason.
- [ ] `Documentation/Wiki/Differential-Testing.md` states the four variables, the script and both commands, and every statement in it is true of the script as filed.
- [ ] `Gap/` is unchanged.

## Notes

## Log

- 2026-10-23: Created.
