---
id: AF-0561
title: Surviving mutant: AltSvcTransferCache.SwitchedVersion can invert which HTTP version an alt-svc switch uses without a test failing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:SwitchedVersion-eq:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:288:==
task: BL-4709
tasks: BL-4709
found: 2026-11-03
found-at: ef78cb0569375bf8475eb1a2da931cfd224d52f4
scorecard: 2026-11-03_0025.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1017.md
---
# AF-0561 - Surviving mutant: AltSvcTransferCache.SwitchedVersion can invert which HTTP version an alt-svc switch uses without a test failing

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:288`: Surviving mutant: AltSvcTransferCache.SwitchedVersion can invert which HTTP version an alt-svc switch uses without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_0025.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:288`

Mutant 'match.SourceAlpn == match.Entry.DestinationAlpn' -> '!=' survived. The mutant keeps the requested version when the destination ALPN differs, and forces HTTP/3-only or HTTP/1.1 when it is the same, so an alt-svc h2->h3 switch would not use HTTP/3 and the request bytes and protocol change.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/AltSvcTransferCache.cs:288:== -Member SwitchedVersion -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

- 2026-11-03 | 2026-11-03_1017.md | reproduces: no | Ran the -Site command twice: outcome killed both times (resolvedLine 288). Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1017.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
