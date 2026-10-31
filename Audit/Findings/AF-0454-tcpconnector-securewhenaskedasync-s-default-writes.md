---
id: AF-0454
title: TcpConnector.SecureWhenAskedAsync's default writesSslFilterAdded = false can become true with no test failing, adding an SSL-filter line to -v output after every CONNECT tunnel
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-31_0358.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/TcpConnector.cs:SecureWhenAskedAsync-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/TcpConnector.cs:2169:false
task: BL-4177
tasks: BL-4177
found: 2026-10-29
found-at: ce4fb84772365b99da350fd339cc0c2e9cb2ce30
scorecard: 2026-10-29_0606.md
duplicate-of:
closed: 2026-10-31
closed-how: mechanical
closed-by: 2026-10-31_0358.md
---
# AF-0454 - TcpConnector.SecureWhenAskedAsync's default writesSslFilterAdded = false can become true with no test failing, adding an SSL-filter line to -v output after every CONNECT tunnel

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/TcpConnector.cs:2169`: TcpConnector.SecureWhenAskedAsync's default writesSslFilterAdded = false can become true with no test failing, adding an SSL-filter line to -v output after every CONNECT tunnel. Reported by an auditor flagged unreliable in 2026-10-29_0606.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/TcpConnector.cs:2169`

Line 2169: `bool writesSslFilterAdded = false)`. Every other caller passes WritesSslFilterAddedFor(target), which is `TracesSetupFilter && IsHttpsOrigin(target)` (line 850). Only the tunnel path at line 1726 (`return await SecureWhenAskedAsync(dialed with { Connection = tunnelConnection }, tunnel.Target, timings, statusCode, cancellationToken)`) relies on the default. With the default flipped to true, AuthenticateTargetAsync (line 2391) calls `target.Events.ReportInfo(SslFilterAddedLine)`, so `* [SETUP] added SSL filter for origin` appears in the verbose output of every transfer through an HTTP CONNECT tunnel, even with no --trace-config. The sampled mutation run (seed 0) left this mutant surviving: no Curl.Networking.UnitTests test checks the tunnel path's verbose lines without setup tracing.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/TcpConnector.cs:2169:false -Member SecureWhenAskedAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed: a tunnel test without setup tracing sees no SSL-filter-added line.
- Actual: outcome survived: every Curl.Networking.UnitTests test passed with the mutant.

## Re-audits

- 2026-10-31 | 2026-10-31_0358.md | reproduces: no | Ran the -Site reproduction: TcpConnector.cs:2169 `bool writesSslFilterAdded = false` -> `true`, outcome killed (resolvedLine 2169; baseline excluded the 3 failing ECH tests). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-29: filed proposed.
- 2026-10-29: proposed -> accepted.
- 2026-10-31: accepted -> closed. Re-audit 2026-10-31_0358.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
