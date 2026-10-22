---
id: AF-0318
title: HttpProxyTunnel.ReplyAfterLineEnd's lineStart == 0 can become != 0 with no test failing, so a CONNECT reply whose first line is not HTTP/ no longer fails at once with exit 43
auditor: quality
severity: High
status: accepted
reason:
key: quality:Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:ReplyAfterLineEnd-eq:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:221:==
task: none
tasks:
found: 2026-10-22
found-at: 167e765b2a009a4887fc971075283ef9d24a73a1
scorecard: 2026-10-22_0130.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0318 - HttpProxyTunnel.ReplyAfterLineEnd's lineStart == 0 can become != 0 with no test failing, so a CONNECT reply whose first line is not HTTP/ no longer fails at once with exit 43

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:221`: HttpProxyTunnel.ReplyAfterLineEnd's lineStart == 0 can become != 0 with no test failing, so a CONNECT reply whose first line is not HTTP/ no longer fails at once with exit 43.

## Evidence

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:221`

Sampled mutant (seed 0) survived: 'if (lineStart == 0 && !StartsWithHttpVersion(header)) return HttpProxyTunnelReply.Failed(InvalidResponseHeader) with { FailureExitCode = CurlExitCode.BadFunctionArgument };' with == turned into !=. This is the measured curl 8.21.0 behaviour (upstream test750, BL-1857): exit 43 'Invalid response header' once the first line ends. Under the mutant, the first line is never checked. A one-line garbage reply followed by an empty line goes to ParseReply instead of the exit 43 failure. A garbage first line from a proxy that then stalls waits for more bytes instead of failing. Either way the exit code and stderr differ, and the Networking twin's tests did not notice.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:221:== -Member ReplyAfterLineEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-22: filed proposed.
- 2026-10-22: proposed -> accepted.
