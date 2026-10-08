---
id: BL-1703
title: Fix AF-0060: HttpChunkedDecoder.AppendToTrailerLine: the trailer line limit 'Length + 1 >= MaximumTrailerLineLength' can become > with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary]
requirement: none
created: 2026-10-08
completed:
---
# BL-1703 — Fix AF-0060: HttpChunkedDecoder.AppendToTrailerLine: the trailer line limit 'Length + 1 >= MaximumTrailerLineLength' can become > with no test failing

## Goal

The defect the audit office reported as AF-0060 is fixed: its reproduction no longer reproduces, so a re-audit can close the finding.

## Context

Filed from audit finding AF-0060 (High, quality auditor), which Stewart accepted. The finding is `Audit/Findings/AF-0060-httpchunkeddecoder-appendtotrailerline-the-trailer.md`.

Location: `Curl.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs:221`

Location: `Curl.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs:221`

Mutant: if (trailerLine.Length + 1 >= MaximumTrailerLineLength) -> if (trailerLine.Length + 1 > MaximumTrailerLineLength), with MaximumTrailerLineLength = 4096. It survived the sampled run (seed 0). A chunked trailer line exactly at the boundary is then accepted instead of failing with CurlExitCode.TooLarge and TrailerTooLarge, which changes the exit code and output a user sees. No test pins the boundary length.

Reproduction, from the finding:

Run from the repository root:

```powershell
powershell -NoProfile -File Audit/Tools/Invoke-MutationTest.ps1 -Site Curl.Protocol.Http.UnitLibrary/HttpChunkedDecoder.cs:221:>= -Member AppendToTrailerLine -ExcludeBaselineFailures -TimeoutSeconds 600
```

- Expected: outcome killed
- Actual: outcome survived

The finding closes only when a later re-audit by the quality auditor confirms the fix, never because this task reaches Done.

## Acceptance criteria

- [ ] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [ ] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
