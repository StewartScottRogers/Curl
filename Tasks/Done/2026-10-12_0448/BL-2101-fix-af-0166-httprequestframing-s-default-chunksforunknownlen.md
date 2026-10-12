---
id: BL-2101
title: Fix AF-0166: HttpRequestFraming's default chunksForUnknownLength = false can become true with no test failing: a bodyless GET's auth retry to an HTTP/1.0 server fails with exit 25
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-12
completed: 2026-10-12
---
# BL-2101 — Fix AF-0166: HttpRequestFraming's default chunksForUnknownLength = false can become true with no test failing: a bodyless GET's auth retry to an HTTP/1.0 server fails with exit 25

## Goal

The defect the audit office reported as AF-0166 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0166 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0166-httprequestframing-s-default-chunksforunknownlengt.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:45`

Location: `Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:45`

Invoke-MutationTest.ps1 -Library Curl.Protocol.Http.UnitLibrary -MaxMutants 40 -Seed 0: mutant HttpRequestFraming.cs:45 'bool chunksForUnknownLength = false)' -> 'bool chunksForUnknownLength = true)' survived. The bodyless framing (HttpRequestFraming.cs:234, 'new HttpRequestFraming(options.CustomMethod ?? (noBody ? "HEAD" : "GET"), null, null, false, false, false)') relies on that default. ChunksForUnknownLength drives ResendsChunkedToHttp10 (HttpProtocolHandler.cs:2639), and RetryOfAsync calls ThrowIfChunkedResendToHttp10 (lines 2341, 2348) before every auth or proxy-auth retry; that throws HttpTransferException(CurlExitCode.UploadFailed, ChunkedUploadNeedsHttp11). Under the mutant, a plain GET with -u/--digest against an HTTP/1.0 server that answers 401 or 407 with a challenge fails with exit 25 'Chunky upload is not supported by HTTP 1.0' instead of retrying with credentials. No test pins the bodyless framing's flag or that retry.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpRequestFraming.cs:45:false -Member HttpRequestFraming -ExcludeBaselineFailures -TimeoutSeconds 120
```

- Expected: outcome killed: a test fails when a bodyless request's framing claims to chunk an unknown length
- Actual: outcome survived: every Curl.Protocol.Http.UnitTests test passed with the mutant

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- Added `HttpRequestFramingTests.Of_NoBody_NeverChunksForUnknownLength` (bodyless GET and HEAD over -0, GET over 1.1): pins `ChunksForUnknownLength` and `IsChunked` false. The lane audit guard refuses running `Audit/Tools/Invoke-MutationTest.ps1`, so the mutant was applied by hand (`= true` at HttpRequestFraming.cs:45): the -0 GET and -0 HEAD rows failed, then the mutant was reverted. A re-audit should confirm with the finding's own command.

## Log

- 2026-10-12: Created.
- 2026-10-12: Backlog -> Doing.
- 2026-10-12: Doing -> Done. Bodyless framing's ChunksForUnknownLength pinned by a test that kills the AF-0166 mutant
