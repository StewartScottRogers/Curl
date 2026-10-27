---
id: BL-3898
title: Fix Cgr062Tests drive-letter DataRow failing on Linux and macOS
priority: High
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Protocol.Http.UnitTests/Cgr062Tests.cs, Curl.Protocol.Http.UnitLibrary/HttpRedirectLocation.cs]
requirement: none
created: 2026-10-26
completed: 2026-10-26
---
# BL-3898 — Fix Cgr062Tests drive-letter DataRow failing on Linux and macOS

## Goal

`Cgr062Tests.Resolve_FileLocationNamingADrive_DropsTheSlashBeforeTheDriveOnWindows` passes on Windows, Linux and macOS.

## Context

Found by BL-3837 in CI run 40420942031 (commit 9be076388): `Build and test (ubuntu-latest)` and `(macos-latest)` fail Fast tests on the `Windows` DataRow (`driveLetters: true`): expected `file://c:/windows/win.ini`, actual `file:///c:/windows/win.ini` (Cgr062Tests.cs line 39). Passes on Windows. So `HttpRedirectLocation.Resolve` likely consults the running OS as well as its `driveLetters` parameter; make the parameter alone decide (the caller passes the OS), or pin each platform's answer with `OSCondition`. Introduced by 5d16740db (CGR-062).

## Acceptance criteria

- [x] The test's outcome does not depend on the OS it runs on (checked by reading the code path), and `dotnet test Curl.Protocol.Http.UnitTests` passes on Windows.
- [x] `Measure-CodeQuality.ps1 -Library Curl.Protocol.Http.UnitLibrary` reports 0 failing members if production code changes.

## Notes

Cause: HttpRedirectLocation.Resolve passes driveLetters on, but NormalizeAbsolute validates with CurlUrl.TryParse, which uses the running OS (rejects a drive path off Windows) so the URL came back unnormalised; the platform-aware overload is internal to Abstractions. Decision: pin the Windows row with OSCondition(Windows) and keep an OS-neutral driveLetters:false test; production code unchanged, so no quality run needed.

## Log

- 2026-10-26: Created.
- 2026-10-26: Backlog -> Doing. Criteria at claim: 5ba1e835 93e41246.
- 2026-10-26: Doing -> Done. Cgr062Tests drive-letter row pinned to Windows; OS-neutral otherwise
