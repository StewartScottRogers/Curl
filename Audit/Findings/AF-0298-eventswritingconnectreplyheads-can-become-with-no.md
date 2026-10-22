---
id: AF-0298
title: EventsWritingConnectReplyHeads' && can become || with no test failing, wrapping a null header output for every tunnel without -i/-D
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-10-22_0130.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/TransferContextFactory.cs:EventsWritingConnectReplyHeads-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/TransferContextFactory.cs:592:&&
task: none
tasks:
found: 2026-10-19
found-at: 1b3c70b1436b2d499099c5c6dd7bb119e11f5631
scorecard: 2026-10-19_2134.md
duplicate-of:
closed: 2026-10-22
closed-how: mechanical
closed-by: 2026-10-22_0130.md
---
# AF-0298 - EventsWritingConnectReplyHeads' && can become || with no test failing, wrapping a null header output for every tunnel without -i/-D

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/TransferContextFactory.cs:592`: EventsWritingConnectReplyHeads' && can become || with no test failing, wrapping a null header output for every tunnel without -i/-D.

## Evidence

Location: `Curl.Engine.UnitLibrary/TransferContextFactory.cs:592`

Sampled mutant (seed 0) survived: IsHttpOrHttpsProxy(proxy) && headerOutput is not null && !proxyOptions.SuppressConnectHeaders => IsHttpOrHttpsProxy(proxy) && headerOutput is not null || !proxyOptions.SuppressConnectHeaders. With the default SuppressConnectHeaders = false, the condition is now always true. So a transfer through an HTTP(S) proxy tunnel with no header output (no -i, -I or -D, the common case) gets ConnectReplyHeadWritingEvents(events, null), whose WriteConnectReplyHeadAsync calls headerOutput.WriteAsync (ConnectReplyHeadWritingEvents.cs:26): a NullReferenceException on the CONNECT reply, so the exit code and output change. No test runs the context factory over a proxy tunnel without header output.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/TransferContextFactory.cs:592:&& -Member EventsWritingConnectReplyHeads -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-10-22 | 2026-10-22_0130.md | reproduces: no | Ran the -Site reproduction: the site moved; resolved within EventsWritingConnectReplyHeads to line 597, mutant Curl.Engine.UnitLibrary/TransferContextFactory.cs:597:&& killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-10-19: filed proposed.
- 2026-10-22: proposed -> closed. Re-audit 2026-10-22_0130.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
