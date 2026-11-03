---
id: AF-0591
title: Surviving mutant: a gRPC-Web trailer line starting with ':' can be refused as having no colon without a test failing
auditor: quality
severity: Medium
status: accepted
reason: Accepted under Stewart's standing accept of 2026-11-02.
key: quality:Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebTrailers.cs:Parse-lt:surviving-mutant
reproduction: mutation Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebTrailers.cs:27:<
task: BL-4755
tasks: BL-4755
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0591 - Surviving mutant: a gRPC-Web trailer line starting with ':' can be refused as having no colon without a test failing

## Summary

Medium finding from the quality auditor at `Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebTrailers.cs:27`: Surviving mutant: a gRPC-Web trailer line starting with ':' can be refused as having no colon without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebTrailers.cs:27`

Drawn library (seed 985570). if (colon < 0) -> if (colon <= 0) survived: a trailer line ':x' is then refused with FormatException instead of read as a field with an empty name. GrpcWebTrailersTests covers only a line with no colon at all.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebTrailers.cs:27:< -Member Parse -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
