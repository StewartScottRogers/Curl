---
id: AF-0692
title: Surviving mutant: any write error (or any header callback that delivered 0) can drop the head from getinfo
auditor: quality
severity: Medium
status: closed
reason: Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:HeaderCallbackTookNothing-and:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:239:&&
task: BL-4886
tasks: BL-4886
found: 2026-11-05
found-at: 4eba3c63638befcc6a63aaa31ddf5419922fa939
scorecard: 2026-11-05_2025.md
duplicate-of:
closed: 2026-11-12
closed-how: mechanical
closed-by: 2026-11-12_2105.md
---
# AF-0692 - Surviving mutant: any write error (or any header callback that delivered 0) can drop the head from getinfo

## Summary

Medium finding from the quality auditor at `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:239`: Surviving mutant: any write error (or any header callback that delivered 0) can drop the head from getinfo.

## Evidence

Location: `Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:239`

'result.ExitCode.ToCurlCode() == CurlCode.WriteError && lastHeaderCallbackOutput is { BytesDelivered: 0 }' mutated to '||' survived (Engine sample, seed 982898). HeaderCallbackTookNothing decides whether getinfo drops HeaderSize, ContentType and ContentLengthDownload (line 246, CGR-252). With '||', a body write error alone would zero the header size and content type that libcurl reports.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/ManagedEasyTransfer.Finish.cs:239:&& -Member HeaderCallbackTookNothing -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-12 | 2026-11-12_2105.md | reproduces: no | Ran the -Site command: ManagedEasyTransfer.Finish.cs:239 '&&' -> '||' (resolvedLine 239) was killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-05: filed proposed.
- 2026-11-12: accepted -> closed. Re-audit 2026-11-12_2105.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
