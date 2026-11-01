---
id: AF-0518
title: PerformAsync_UploadWithNoReadCallback_SendsAnEmptyBody checks only the request line, not the empty body
auditor: quality
severity: Low
status: accepted
reason: Stewart accepted it on 2026-11-01 ("accept all").
key: quality:Curl.Engine.UnitTests/ManagedEasyTransferTests.cs:PerformAsync_UploadWithNoReadCallback_SendsAnEmptyBody:weak-assertion
reproduction: none
task: BL-4447
tasks: BL-4447
found: 2026-11-01
found-at: 8dd012a88d26bb19a4e7d95f0be5a3371e3d2b15
scorecard: 2026-11-01_0144.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0518 - PerformAsync_UploadWithNoReadCallback_SendsAnEmptyBody checks only the request line, not the empty body

## Summary

Low finding from the quality auditor at `Curl.Engine.UnitTests/ManagedEasyTransferTests.cs:552`: PerformAsync_UploadWithNoReadCallback_SendsAnEmptyBody checks only the request line, not the empty body.

## Evidence

Location: `Curl.Engine.UnitTests/ManagedEasyTransferTests.cs:552`

The test asserts CurlCode.Ok and StringAssert.StartsWith(request, "PUT /up HTTP/1.1\r\n"). Nothing checks Content-Length: 0 or the bytes after the head, so a transfer that sent a non-empty body would pass.

## Reproduction

Run from the repository root:

```powershell
Select-String -Path Curl.Engine.UnitTests/ManagedEasyTransferTests.cs -Pattern 'SendsAnEmptyBody' -Context 0,12
```

- Expected: The body asserts the whole request (an empty body, e.g. Content-Length: 0).
- Actual: Line 552: StringAssert.StartsWith(Encoding.ASCII.GetString(easyBytes), "PUT /up HTTP/1.1\r\n"); is the only byte check.

## Re-audits

## Log

- 2026-11-01: filed proposed.
- 2026-11-01: proposed -> accepted. Stewart: accept all.
