---
id: AF-0618
title: Surviving mutant: a CONNECT reply whose first byte is NUL can escape the NUL-byte refusal without a test failing
auditor: quality
severity: High
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:ReplyAfterLineEnd-ge:surviving-mutant
reproduction: mutation Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:231:>=
task: BL-4791
tasks: BL-4791
found: 2026-11-03
found-at: 7410f102c809d5232e4f9b210b3ac32486848c27
scorecard: 2026-11-03_1615.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0618 - Surviving mutant: a CONNECT reply whose first byte is NUL can escape the NUL-byte refusal without a test failing

## Summary

High finding from the quality auditor at `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:231`: Surviving mutant: a CONNECT reply whose first byte is NUL can escape the NUL-byte refusal without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1615.md.

## Evidence

Location: `Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:231`

Mutant 'if (header.IndexOf(0, lineStart) >= 0)' -> '> 0' survived. IndexOf returns an absolute index, so the mutant differs only when the NUL sits at index 0, the reply's first byte. That reply then skips NulByteReply and is parsed as a status line, so the proxy tunnel's exit code and message change. No HttpProxyTunnelTests case starts the reply with a NUL.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Networking.UnitLibrary/HttpProxyTunnel.cs:231:>= -Member ReplyAfterLineEnd -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
