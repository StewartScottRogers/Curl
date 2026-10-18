---
id: BL-2773
title: Fix AF-0262: FormatLibraryVersion's doc comment gives libcurl/8.21.0 as the -V library text; the method returns libcurl/8.22.0
priority: Normal
assignee: Claude
pipeline: docs
depends-on: []
touches: [Curl.Output.UnitLibrary]
requirement: none
created: 2026-10-18
completed:
---
# BL-2773 — Fix AF-0262: FormatLibraryVersion's doc comment gives libcurl/8.21.0 as the -V library text; the method returns libcurl/8.22.0

## Goal

The defect the audit office reported as AF-0262 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0262 (Medium, truthfulness auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0262-formatlibraryversion-s-doc-comment-gives-libcurl-8.md`.

Location: `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs:308`

Location: `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs:308`

Lines 306-310 of the summary: 'The library part of -V's first line ... <c>libcurl/8.21.0 Schannel</c> on Windows, <c>libcurl/8.21.0 SecureTransport</c> on macOS and <c>libcurl/8.21.0 OpenSSL</c> anywhere else.' The body (lines 317-319) returns "libcurl/8.22.0 Schannel", "libcurl/8.22.0 SecureTransport" and "libcurl/8.22.0 OpenSSL", and Curl.Output.UnitTests/TransferWriteOutVariablesJsonTests.cs:19 pins libcurl/8.22.0. The doc states exact output bytes, so an agent writing a test or a write-out expectation from it would pin the wrong version. Phase 2: ADR-0479 (curl retargets from 8.21.0 to 8.22.0) moved the code but not this summary.

Reproduction, from the finding:

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitLibrary/TransferWriteOutVariables.cs -Pattern 'libcurl/8\.2[12]\.0 Schannel' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }
```

- Expected: Doc line and code line name the same version, libcurl/8.22.0 Schannel.
- Actual: 308: /// <c>SslStream</c> sits on: <c>libcurl/8.21.0 Schannel</c> on Windows,  |  317: return isWindows ? "libcurl/8.22.0 Schannel"

The finding closes only when a later re-audit by the truthfulness auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-18: Created.
