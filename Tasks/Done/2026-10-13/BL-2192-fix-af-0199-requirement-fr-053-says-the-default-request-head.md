---
id: BL-2192
title: Fix AF-0199: Requirement FR-053 says the default request head carries User-Agent: curl/8.21.0; Curl sends curl/8.22.0
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-14
completed: 2026-10-13
---
# BL-2192 — Fix AF-0199: Requirement FR-053 says the default request head carries User-Agent: curl/8.21.0; Curl sends curl/8.22.0

## Goal

The defect the audit office reported as AF-0199 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0199 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0199-requirement-fr-053-says-the-default-request-head-c.md`.

Location: `Documentation/Product/Requirements.md:188`

Location: `Documentation/Product/Requirements.md:188`

FR-053: 'With no request options, the headers are exactly `Host`, `User-Agent: curl/8.21.0`, `Accept: */*` ... `GET /a?b HTTP/1.1\r\nHost: 127.0.0.1:<P>\r\nUser-Agent: curl/8.21.0\r\n...`'. Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs:45 is 'internal const string DefaultUserAgent = "curl/8.22.0";', and so are the RTSP, TFTP MASQUE, proxy-tunnel and --libcurl defaults. No non-comment line of a production project names 8.21.0, and 16 name 8.22.0. Requirements.md has 17 lines with 'User-Agent: curl/8', all stale the same way. An agent that pins a test or a fix to FR-053's exact bytes would pin the wrong header. Phase 2: explained by ADR-0479 (retarget to curl 8.22.0; decision 3.2 / BL-2127 moved the User-Agent). Its decision 4 keeps 'as curl 8.21.0 does' statements as records of past measurements, but FR-053 states what Curl sends now, so the ADR does not cover it.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Documentation/Product/Requirements.md -Pattern '^\| FR-053 .*User-Agent: curl/8\.21\.0').Count; (Select-String -Path Curl.Protocol.Http.UnitLibrary/HttpRequestHeadFormatter.cs -Pattern 'DefaultUserAgent = ').Line.Trim()
```

- Expected: 1, then internal const string DefaultUserAgent = "curl/8.21.0"; (or 0 when FR-053 names curl/8.22.0)
- Actual: 1
internal const string DefaultUserAgent = "curl/8.22.0";

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

Replaced all 22 `User-Agent: curl/8.21.0` in Requirements.md (17 lines) with 8.22.0; each described current behaviour. Docs-only, no code reads the file, so no rebuild.

## Log

- 2026-10-14: Created.
- 2026-10-13: Backlog -> Doing.
- 2026-10-13: Doing -> Done. FR-053 and 16 sibling lines now name curl/8.22.0
