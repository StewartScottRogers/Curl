---
id: BL-3218
title: Fix AF-0365: ManagedCurlUrlHandle.Login's || can become && with no test failing, so a URL with only a password loses its login when read back
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs]
requirement: none
created: 2026-10-23
completed:
---
# BL-3218 — Fix AF-0365: ManagedCurlUrlHandle.Login's || can become && with no test failing, so a URL with only a password loses its login when read back

## Goal

The defect the audit office reported as AF-0365 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0365 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0365-managedcurlurlhandle-login-s-can-become-with-no-te.md`.

Location: `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:225`

Location: `Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:225`

Invoke-MutationTest -Library Curl.Engine.UnitLibrary -MaxMutants 40 -Seed 0: survived. Original: bool any = parts.User is not null || parts.Password is not null || options is not null; mutated: parts.User is not null || parts.Password is not null && options is not null. With a password and no user or options (http://:pw@host/), `any` becomes false and curl_url_get(CURLUPART_URL) drops the `:pw@` login from the URL text it returns. The URL API's output changes and ManagedCurlUrlHandleTests do not notice.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedCurlUrlHandle.cs:225:|| -Member Login -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-23: Created.
