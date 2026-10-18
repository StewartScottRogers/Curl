---
id: AF-0262
title: FormatLibraryVersion's doc comment gives libcurl/8.21.0 as the -V library text; the method returns libcurl/8.22.0
auditor: truthfulness
severity: Medium
status: accepted
reason: 
key: truthfulness:Curl.Output.UnitLibrary/TransferWriteOutVariables.cs:FormatLibraryVersion:false-doc-comment
reproduction: none
task: BL-2773
tasks: BL-2773
found: 2026-10-18
found-at: 19ea231be8b4d749462d15058a3c6b97c7e70836
scorecard: 2026-10-18_1045.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0262 - FormatLibraryVersion's doc comment gives libcurl/8.21.0 as the -V library text; the method returns libcurl/8.22.0

## Summary

Medium finding from the truthfulness auditor at `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs:308`: FormatLibraryVersion's doc comment gives libcurl/8.21.0 as the -V library text; the method returns libcurl/8.22.0.

## Evidence

Location: `Curl.Output.UnitLibrary/TransferWriteOutVariables.cs:308`

Lines 306-310 of the summary: 'The library part of -V's first line ... <c>libcurl/8.21.0 Schannel</c> on Windows, <c>libcurl/8.21.0 SecureTransport</c> on macOS and <c>libcurl/8.21.0 OpenSSL</c> anywhere else.' The body (lines 317-319) returns "libcurl/8.22.0 Schannel", "libcurl/8.22.0 SecureTransport" and "libcurl/8.22.0 OpenSSL", and Curl.Output.UnitTests/TransferWriteOutVariablesJsonTests.cs:19 pins libcurl/8.22.0. The doc states exact output bytes, so an agent writing a test or a write-out expectation from it would pin the wrong version. Phase 2: ADR-0479 (curl retargets from 8.21.0 to 8.22.0) moved the code but not this summary.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Output.UnitLibrary/TransferWriteOutVariables.cs -Pattern 'libcurl/8\.2[12]\.0 Schannel' | ForEach-Object { '{0}: {1}' -f $_.LineNumber, $_.Line.Trim() }
```

- Expected: Doc line and code line name the same version, libcurl/8.22.0 Schannel.
- Actual: 308: /// <c>SslStream</c> sits on: <c>libcurl/8.21.0 Schannel</c> on Windows,  |  317: return isWindows ? "libcurl/8.22.0 Schannel"

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-18: proposed -> accepted.
