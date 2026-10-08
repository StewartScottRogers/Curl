---
id: AF-0060
title: HttpChunkedDecoder.AppendToTrailerLine: the trailer line limit 'Length + 1 >= MaximumTrailerLineLength' can become > with no test failing
auditor: quality
severity: High
status: proposed
reason:
key: quality:Curl.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs:AppendToTrailerLine-ge:surviving-mutant
reproduction: mutation Curl.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs:221:>=
task: none
tasks:
found: 2026-10-07
found-at: 0fcb5afc262ef32bb48ad058cf1f4a2b2c68d511
scorecard: 2026-10-07_1336.md
duplicate-of:
closed:
closed-how:
closed-by:
---
# AF-0060 - HttpChunkedDecoder.AppendToTrailerLine: the trailer line limit 'Length + 1 >= MaximumTrailerLineLength' can become > with no test failing

## Summary

High finding from the quality auditor at `Curl.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs:221`: HttpChunkedDecoder.AppendToTrailerLine: the trailer line limit 'Length + 1 >= MaximumTrailerLineLength' can become > with no test failing.

## Evidence

Location: `Curl.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs:221`

Mutant: if (trailerLine.Length + 1 >= MaximumTrailerLineLength) -> if (trailerLine.Length + 1 > MaximumTrailerLineLength), with MaximumTrailerLineLength = 4096. It survived the sampled run (seed 0). A chunked trailer line exactly at the boundary is then accepted instead of failing with CurlExitCode.TooLarge and TrailerTooLarge, which changes the exit code and output a user sees. No test pins the boundary length.

## Reproduction

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs:221:>= -Member AppendToTrailerLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

## Re-audits

## Log

- 2026-10-07: filed proposed.
