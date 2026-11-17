---
id: AF-0826
title: Surviving mutant: a plain CONNECT reply can be read as a CONNECT-UDP one, so a 101 skips its Content-Length
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-17_0430.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:ReadReplyAsync-false:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:163:false
task: BL-5300
tasks: BL-5300
found: 2026-11-16
found-at: f28eba04920fb5d8dd2a1c70283f268a1891b7e5
scorecard: 2026-11-16_1333.md
duplicate-of:
closed: 2026-11-17
closed-how: mechanical
closed-by: 2026-11-17_0430.md
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

- 2026-11-17 | 2026-11-17_0430.md | reproduces: no | Ran the -Site command (run alone; an earlier attempt clashed with a parallel run's worktree): the site resolved to line 163, mutant forConnectUdp: false -> true, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-16: filed proposed.
- 2026-11-17: accepted -> closed. Re-audit 2026-11-17_0430.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
