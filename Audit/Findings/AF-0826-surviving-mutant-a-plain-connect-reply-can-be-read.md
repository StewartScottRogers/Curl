---
id: AF-0826
title: Surviving mutant: a plain CONNECT reply can be read as a CONNECT-UDP one, so a 101 skips its Content-Length
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:ReadReplyAsync-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:163:false
task: none
tasks:
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0826 - Surviving mutant: a plain CONNECT reply can be read as a CONNECT-UDP one, so a 101 skips its Content-Length

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:163`: Surviving mutant: a plain CONNECT reply can be read as a CONNECT-UDP one, so a 101 skips its Content-Length. Reported by an auditor flagged unreliable in 2026-11-16_1333.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:163`

Mutation sample (seed 413266): 'ReadReplyAsync(connection, forConnectUdp: false, cancellationToken);' mutated to 'forConnectUdp: true' survived; every Curl.Networking.UnitTests test passed. TcpConnector's plain CONNECT goes through this overload (TcpConnector.cs:2064). forConnectUdp decides IgnoresBodyFields(statusCode, forConnectUdp) => statusCode / 100 == 2 || (forConnectUdp && statusCode == 101). With the mutant, a proxy answering a plain CONNECT with 101 has its Content-Length ignored: the file's own comment says curl exits 8 at a Content-Length that is not a number unless the status ignores it, so the exit code changes. No test pins a CONNECT answered 101.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:163:false -Member ReadReplyAsync -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-16: filed proposed.
