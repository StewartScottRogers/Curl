---
id: BL-2276
title: Fix AF-0223: Requirements FR-020 says a dict:// transfer sends CLIENT libcurl 8.21.0; the code sends CLIENT libcurl 8.22.0
priority: High
assignee: Claude
pipeline: docs
depends-on: []
touches: [Documentation]
requirement: none
created: 2026-10-15
completed:
---
# BL-2276 — Fix AF-0223: Requirements FR-020 says a dict:// transfer sends CLIENT libcurl 8.21.0; the code sends CLIENT libcurl 8.22.0

## Goal

The defect the audit office reported as AF-0223 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0223 (High, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0223-requirements-fr-020-says-a-dict-transfer-sends-cli.md`.

Location: `Documentation/Product/Requirements.md:66`

Location: `Documentation/Product/Requirements.md:66`

FR-020 reads: 'A dict:// transfer sends its whole request without waiting for the server's greeting: `CLIENT libcurl 8.21.0\r\n`, then one command line, then `QUIT\r\n`. Built; every command test in DictProtocolHandlerTests asserts the three lines'. The code sends something else: Curl.Protocol.Dict.UnitLibrary/DictRequest.cs:37 is `public const string ClientLine = "CLIENT libcurl 8.22.0";`, and Curl.Protocol.Dict.UnitTests/DictProtocolHandlerTests.cs:16 pins `CLIENT libcurl 8.22.0\r\n`. An agent taking the requirement as the spec would pin or 'fix' the wrong bytes. The project's own CLAUDE.md already says the CLIENT line moved to 8.22.0 (ADR-0479, BL-2127).

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Documentation/Product/Requirements.md -Pattern 'CLIENT libcurl 8\.21\.0' | ForEach-Object { $_.LineNumber }; (Select-String -Path Curl.Protocol.Dict.UnitLibrary/DictRequest.cs -Pattern 'ClientLine = ').Line.Trim()
```

- Expected: No match in Requirements.md (FR-020 names the CLIENT line DictRequest.ClientLine holds), then: public const string ClientLine = "CLIENT libcurl 8.22.0";
- Actual: 66, then: public const string ClientLine = "CLIENT libcurl 8.22.0";

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-15: Created.
