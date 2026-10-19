---
id: BL-2799
title: Fix AF-0272: CreateFtpProtocolHandler's tracesFtp default can become true with no test failing, tracing connect-only FTP
priority: Normal
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Engine.UnitLibrary]
requirement: none
created: 2026-10-19
completed:
---
# BL-2799 — Fix AF-0272: CreateFtpProtocolHandler's tracesFtp default can become true with no test failing, tracing connect-only FTP

## Goal

The defect the audit office reported as AF-0272 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0272 (Medium, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0272-createftpprotocolhandler-s-tracesftp-default-can-b.md`.

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:230`

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:230`

Mutant bool tracesFtp = false -> true survived (sampled run, seed 0). The default is not equivalent: ManagedCurlEngine.cs:232 builds the CURLOPT_CONNECT_ONLY FTP login handler with EngineComposition.CreateFtpProtocolHandler(connector, connector, transports.TlsProvider, transports.DnsResolver), relying on the default. With the mutant that handler sets TracesStateMachine and writes --trace-config ftp lines nobody asked for into verbose/debug output.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineComposition.cs:230:false -Member CreateFtpProtocolHandler -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-19: Created.
