---
id: AF-0536
title: Surviving mutant: AltSvcTransferCache.SameDestinationVersion can invert which alt-svc entries race HTTP/3 without a test failing
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-03_0025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:SameDestinationVersion-eq:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:268:==
task: BL-4642
tasks: BL-4642
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_0025.md
---
# AF-0536 - Surviving mutant: AltSvcTransferCache.SameDestinationVersion can invert which alt-svc entries race HTTP/3 without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:268`: Surviving mutant: AltSvcTransferCache.SameDestinationVersion can invert which alt-svc entries race HTTP/3 without a test failing. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:268`

Mutant 'destination == AltSvcAlpn.H3 && requestedVersion == HttpVersion.None ? Http3 : version' -> 'destination != AltSvcAlpn.H3 ...' survived (Curl.Engine.UnitTests green). With it, an alt-svc entry naming h3 for the origin no longer races HTTP/3 against TCP (as the doc comment says curl was measured to do), while h2 and h1 entries start a QUIC attempt. The protocol and connections a transfer uses change, with no test noticing.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:268:== -Member SameDestinationVersion -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-03 | 2026-11-03_0025.md | reproduces: no | Ran the -Site command: outcome killed at resolvedLine 268 ('destination == AltSvcAlpn.H3' -> 'destination != AltSvcAlpn.H3'). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_0025.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
