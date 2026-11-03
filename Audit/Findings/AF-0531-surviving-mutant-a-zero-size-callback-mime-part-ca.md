---
id: AF-0531
title: Surviving mutant: a zero-size callback MIME part can lose its known length without a test failing
auditor: quality
severity: High
status: accepted
reason: 
key: quality:Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:ContentLengthOf-ge:surviving-mutant
reproduction: mutation Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:116:>=
task: BL-4637
tasks: BL-4637
found: 2026-11-02
found-at: fcdba3d03e8b5378439deb78f2055b6f5eccf2bb
scorecard: 2026-11-02_1724.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0531 - Surviving mutant: a zero-size callback MIME part can lose its known length without a test failing

## Summary

High finding from the quality auditor at `Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:116`: Surviving mutant: a zero-size callback MIME part can lose its known length without a test failing. Reported by an auditor flagged unreliable in 2026-11-02_1724.md.

## Evidence

Location: `Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:116`

Mutant 'data is EasyMimeCallback { Size: >= 0 } sized ? sized.Size : null' -> '{ Size: > 0 }' survived (Curl.Engine.UnitTests green). A callback part whose CURLOPT/curl_mime_data_cb size is 0 then maps to an unknown length (null), the same as -1. The multipart body's length becomes unknown, and the request is framed differently (chunked or without Content-Length) where curl sends a known Content-Length. No test drives a zero-size callback part, so request bytes can change unnoticed.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Engine.UnitLibrary/MultipartFormPartMapping.cs:116:>= -Member ContentLengthOf -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: killed
- Actual: survived

## Re-audits

## Log

- 2026-11-02: filed proposed.
- 2026-11-02: proposed -> accepted.
