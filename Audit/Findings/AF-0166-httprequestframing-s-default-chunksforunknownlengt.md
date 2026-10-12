---
id: AF-0166
title: HttpRequestFraming's default chunksForUnknownLength = false can become true with no test failing: a bodyless GET's auth retry to an HTTP/1.0 server fails with exit 25
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:HttpRequestFraming-true:surviving-mutant
reproduction: none
task: none
tasks:
found: 2026-10-11
found-at: d8245edb7b661211c6628e9062e9939934939fb2
scorecard: 2026-10-11_1638.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0166 - HttpRequestFraming's default chunksForUnknownLength = false can become true with no test failing: a bodyless GET's auth retry to an HTTP/1.0 server fails with exit 25

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:45`: HttpRequestFraming's default chunksForUnknownLength = false can become true with no test failing: a bodyless GET's auth retry to an HTTP/1.0 server fails with exit 25.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:45`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0: mutant HttpRequestFraming.cs:45 'bool chunksForUnknownLength = false)' -> 'bool chunksForUnknownLength = true)' survived. The bodyless framing (HttpRequestFraming.cs:234, 'new HttpRequestFraming(options.CustomMethod ?? (noBody ? "HEAD" : "GET"), null, null, false, false, false)') relies on that default. ChunksForUnknownLength drives ResendsChunkedToHttp10 (HttpProtocolHandler.cs:2639), and RetryOfAsync calls ThrowIfChunkedResendToHttp10 (lines 2341, 2348) before every auth or proxy-auth retry; that throws HttpTransferException(CurlExitCode.UploadFailed, ChunkedUploadNeedsHttp11). Under the mutant, a plain GET with -u/--digest against an HTTP/1.0 server that answers 401 or 407 with a challenge fails with exit 25 'Chunky upload is not supported by HTTP 1.0' instead of retrying with credentials. No test pins the bodyless framing's flag or that retry.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:45:false -Member HttpRequestFraming -ExcludeBaselineFailures -TimeoutSeconds 120
```

- Expected: outcome killed: a test fails when a bodyless request's framing claims to chunk an unknown length
- Actual: outcome survived: every Curl.Protocol.Http.UnitTests test passed with the mutant

## Re-audits

## Log

- 2026-10-11: filed proposed.
