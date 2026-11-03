---
id: BL-4756
title: Fix AF-0592: Surviving mutant: a gRPC-Web reply ending in an empty frame can be refused as cut short without a test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Extension.GrpcWeb.UnitLibrary]
requirement: none
created: 2026-11-03
completed:
---
# BL-4756 — Fix AF-0592: Surviving mutant: a gRPC-Web reply ending in an empty frame can be refused as cut short without a test failing

## Goal

The defect the audit office reported as AF-0592 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0592 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0592-surviving-mutant-a-grpc-web-reply-ending-in-an-emp.md`.

Location: `Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67`

Location: `Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67`

Drawn library. rest.Length < PrefixLength -> rest.Length <= PrefixLength survived: a final 5-byte frame with length 0 (an empty message or an empty trailers frame) then reads its length as long.MaxValue and throws 'the reply ends inside a frame', failing a valid reply with a different exit code. No test parses a body that ends with a zero-length frame.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Extension.GrpcWeb.UnitLibrary/GrpcWebFraming.cs:67:< -Member PayloadAt -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-11-03: Created.
