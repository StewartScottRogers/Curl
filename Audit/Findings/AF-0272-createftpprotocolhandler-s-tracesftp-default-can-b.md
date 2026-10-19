---
id: AF-0272
title: CreateFtpProtocolHandler's tracesFtp default can become true with no test failing, tracing connect-only FTP
auditor: quality
severity: Medium
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/EngineComposition.cs:CreateFtpProtocolHandler-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EngineComposition.cs:230:false
task: none
tasks:
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0272 - CreateFtpProtocolHandler's tracesFtp default can become true with no test failing, tracing connect-only FTP

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/EngineComposition.cs:230`: CreateFtpProtocolHandler's tracesFtp default can become true with no test failing, tracing connect-only FTP. Reported by an auditor flagged unreliable in 2026-10-18_1905.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/EngineComposition.cs:230`

Mutant bool tracesFtp = false -> true survived (sampled run, seed 0). The default is not equivalent: ManagedCurlEngine.cs:232 builds the CURLOPT_CONNECT_ONLY FTP login handler with EngineComposition.CreateFtpProtocolHandler(connector, connector, transports.TlsProvider, transports.DnsResolver), relying on the default. With the mutant that handler sets TracesStateMachine and writes --trace-config ftp lines nobody asked for into verbose/debug output.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/EngineComposition.cs:230:false -Member CreateFtpProtocolHandler -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
