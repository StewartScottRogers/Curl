---
id: BL-4177
title: Fix AF-0454: TcpConnector.SecureWhenAskedAsync's default writesSslFilterAdded = false can become true with no test failing, adding an SSL-filter line to -v output after every CONNECT tunnel
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Networking.UnitLibrary]
requirement: none
created: 2026-10-29
completed:
---
# BL-4177 — Fix AF-0454: TcpConnector.SecureWhenAskedAsync's default writesSslFilterAdded = false can become true with no test failing, adding an SSL-filter line to -v output after every CONNECT tunnel

## Goal

The defect the audit office reported as AF-0454 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0454 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0454-tcpconnector-securewhenaskedasync-s-default-writes.md`.

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2169`

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2169`

Line 2169: `bool writesSslFilterAdded = false)`. Every other caller passes WritesSslFilterAddedFor(target), which is `TracesSetupFilter && IsHttpsOrigin(target)` (line 850). Only the tunnel path at line 1726 (`return await SecureWhenAskedAsync(dialed with { Connection = tunnelConnection }, tunnel.Target, timings, statusCode, cancellationToken)`) relies on the default. With the default flipped to true, AuthenticateTargetAsync (line 2391) calls `target.Events.ReportInfo(SslFilterAddedLine)`, so `* [SETUP] added SSL filter for origin` appears in the verbose output of every transfer through an HTTP CONNECT tunnel, even with no --trace-config. The sampled mutation run (seed 0) left this mutant surviving: no Curl.Networking.UnitTests test checks the tunnel path's verbose lines without setup tracing.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:2169:false -Member SecureWhenAskedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a tunnel test without setup tracing sees no SSL-filter-added line.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with the mutant.

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-29: Created.
