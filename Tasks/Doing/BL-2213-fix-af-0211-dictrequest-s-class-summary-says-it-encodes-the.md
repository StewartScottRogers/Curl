---
id: BL-2213
title: Fix AF-0211: DictRequest's class summary says it encodes the bytes curl 8.21.0 sends; its CLIENT line is libcurl 8.22.0
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Dict.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2213 — Fix AF-0211: DictRequest's class summary says it encodes the bytes curl 8.21.0 sends; its CLIENT line is libcurl 8.22.0

## Goal

The defect the audit office reported as AF-0211 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0211 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0211-dictrequest-s-class-summary-says-it-encodes-the-by.md`.

Location: `Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:8`

Location: `Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:8`

Line 8: '/// Encodes a <c>dict</c> URL's path as the bytes curl 8.21.0 sends: a <c>CLIENT</c> line, ...'. Line 37: public const string ClientLine = "CLIENT libcurl 8.22.0"; and line 34 says the bytes match curl 8.22.0 (ADR-0479). curl 8.21.0 sends CLIENT libcurl 8.21.0, so the encoded bytes are not what curl 8.21.0 sends. This is the same leftover AF-0201 fixed on ClientLine's own summary, here on the class summary.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Protocol.Dict.UnitLibrary/DictRequest.cs -Pattern 'as the bytes curl 8\.21\.0 sends','ClientLine = '
```

- Expected: The class summary names the same curl version as ClientLine (8.22.0), or no 8.21.0 match.
- Actual: DictRequest.cs:8:/// Encodes a <c>dict</c> URL's path as the bytes curl 8.21.0 sends: a <c>CLIENT</c> line, | DictRequest.cs:37:    public const string ClientLine = "CLIENT libcurl 8.22.0";

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-14: Backlog -> Doing.
