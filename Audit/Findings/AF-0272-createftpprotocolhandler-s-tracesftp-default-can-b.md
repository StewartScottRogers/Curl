---
id: AF-0272
title: CreateFtpProtocolHandler's tracesFtp default can become true with no test failing, tracing connect-only FTP
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/EngineComposition.cs:CreateFtpProtocolHandler-false:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/EngineComposition.cs:230:false
task: BL-2799
tasks: BL-2799
found: 2026-10-18
found-at: e17dfce54cf6f2d08fc7c4abe0655128f237ff3b
scorecard: 2026-10-18_1905.md
duplicate-of:
closed: 2026-10-19
closed-how: mechanical
closed-by: 2026-10-19_2134.md
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

- 2026-10-19 | 2026-10-19_2134.md | reproduces: no | Ran the -Site command: EngineComposition.cs:230:false resolved to line 237 in CreateFtpProtocolHandler; outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-18: filed proposed.
- 2026-10-19: proposed -> accepted.
- 2026-10-19: accepted -> closed. Re-audit 2026-10-19_2134.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
