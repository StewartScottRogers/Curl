---
id: BL-2193
title: Fix AF-0200: Dict CLAUDE.md says DictRequest.ClientLine names libcurl 8.21.0; it is CLIENT libcurl 8.22.0
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Protocol.Dict.UnitLibrary]
requirement: none
created: 2026-10-14
completed:
---
# BL-2193 — Fix AF-0200: Dict CLAUDE.md says DictRequest.ClientLine names libcurl 8.21.0; it is CLIENT libcurl 8.22.0

## Goal

The defect the audit office reported as AF-0200 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0200 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0200-dict-claude-md-says-dictrequest-clientline-names-l.md`.

Location: `Curl.Protocol.Dict.UnitLibrary/CLAUDE.md:27`

Location: `Curl.Protocol.Dict.UnitLibrary/CLAUDE.md:27`

The Layout section says: 'Its `ClientLine` constant names `libcurl 8.21.0`, as upstream sends it.' Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:37 is 'public const string ClientLine = "CLIENT libcurl 8.22.0";', and every Dict test expects 'CLIENT libcurl 8.22.0\r\n' (DictProtocolHandlerTests.cs:16, DictProtocolHandlerTransferEventsTests.cs:26-28, among others). The file also says 'Every byte these classes send was measured against curl 8.21.0', which no longer holds for the CLIENT line. Phase 2: explained by ADR-0479 (decision 3.2 / BL-2127: DICT reports 8.22.0). Decision 4 keeps measurement statements, but this sentence states the constant's current value.

Reproduction, from the finding:

Run from the repository root:

```powershell
(Select-String -Path Curl.Protocol.Dict.UnitLibrary/CLAUDE.md -Pattern 'names .libcurl 8\.21\.0').Count; (Select-String -Path Curl.Protocol.Dict.UnitLibrary/DictRequest.cs -Pattern 'ClientLine = ').Line.Trim()
```

- Expected: 1, then public const string ClientLine = "CLIENT libcurl 8.21.0"; (or 0 when the CLAUDE.md names 8.22.0)
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
