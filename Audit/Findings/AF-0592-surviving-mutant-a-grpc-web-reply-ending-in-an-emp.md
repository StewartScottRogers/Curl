---
id: AF-0592
title: Surviving mutant: a gRPC-Web reply ending in an empty frame can be refused as cut short without a test failing
auditor: quality
severity: High
status: closed
reason: Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
key: quality:Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:PayloadAt-lt:surviving-mutant
reproduction: mutation Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67:<
task: BL-4756
tasks: BL-4756
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed: 2026-11-03
closed-how: mechanical
closed-by: 2026-11-03_1615.md
---
# AF-0592 - Surviving mutant: a gRPC-Web reply ending in an empty frame can be refused as cut short without a test failing

## Summary

High finding from the quality auditor at `Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67`: Surviving mutant: a gRPC-Web reply ending in an empty frame can be refused as cut short without a test failing. Reported by an auditor flagged unreliable in 2026-11-03_1017.md.

## Evidence

Location: `Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67`

Drawn library. rest.Length < PrefixLength -> rest.Length <= PrefixLength survived: a final 5-byte frame with length 0 (an empty message or an empty trailers frame) then reads its length as long.MaxValue and throws 'the reply ends inside a frame', failing a valid reply with a different exit code. No test parses a body that ends with a zero-length frame.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67:< -Member PayloadAt -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

- 2026-11-03 | 2026-11-03_1615.md | reproduces: no | Ran the -Site command: GrpcWebFraming.cs:67 < resolved to line 67, outcome killed. Runner's targeted mutation rerun on the clean audited commit: killed.

## Log

- 2026-11-03: filed proposed.
- 2026-11-03: proposed -> accepted. Accepted under Stewart's standing accept of 2026-11-02.
- 2026-11-03: accepted -> closed. Re-audit 2026-11-03_1615.md: the reproduction no longer reproduces, and the runner's targeted mutant was killed on the clean audited commit.
