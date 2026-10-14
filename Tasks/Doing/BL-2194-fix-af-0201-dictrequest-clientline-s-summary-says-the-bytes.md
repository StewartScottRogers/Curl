---
id: BL-2194
title: Fix AF-0201: DictRequest.ClientLine's summary says the bytes sent match curl 8.21.0; the constant is CLIENT libcurl 8.22.0
priority: Low
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Dict.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2194 — Fix AF-0201: DictRequest.ClientLine's summary says the bytes sent match curl 8.21.0; the constant is CLIENT libcurl 8.22.0

## Goal

The defect the audit office reported as AF-0201 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0201 (Low, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0201-dictrequest-clientline-s-summary-says-the-bytes-se.md`.

Location: `Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:33`

Location: `Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:33`

The summary says: 'It names upstream curl's own version so the bytes sent match curl 8.21.0; it is one constant so it can follow the version this project measures against.' The constant directly below (line 37) is "CLIENT libcurl 8.22.0", which curl 8.21.0 would not send. The intent (follow the measured version) is right; the version named in it is stale. Phase 2: explained by ADR-0479 (BL-2127 moved the DICT client line to 8.22.0).

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Protocol.Dict.UnitLibrary/DictRequest.cs -Pattern 'so the bytes sent match curl 8\.21\.0').Count; (Select-String -Path Curl.Protocol.Dict.UnitLibrary/DictRequest.cs -Pattern 'ClientLine = ').Line.Trim()
```

- Expected: 0 (summary names the version the constant holds), or a constant naming 8.21.0
- Actual: 1
public const string ClientLine = "CLIENT libcurl 8.22.0";

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-14: Created.
- 2026-10-13: Backlog -> Doing.
