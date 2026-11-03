---
id: AF-0592
title: Surviving mutant: a gRPC-Web reply ending in an empty frame can be refused as cut short without a test failing
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:PayloadAt-lt:surviving-mutant
reproduction: mutation Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67:<
task: none
tasks:
found: 2026-11-03
found-at: f4a738a932020f9832b23c0977dbaf0f27206871
scorecard: 2026-11-03_1017.md
duplicate-of:
closed:
closed-how:
closed-by:
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

## Log

- 2026-11-03: filed proposed.
