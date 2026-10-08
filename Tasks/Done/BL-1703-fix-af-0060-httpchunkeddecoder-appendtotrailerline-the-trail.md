---
id: BL-1703
title: Fix AF-0060: HttpChunkedDecoder.AppendToTrailerLine: the trailer line limit 'Length + 1 >= MaximumTrailerLineLength' can become > with no test failing
priority: High
assignee: Claude
pipeline: feature
depends-on: []
touches: [Curl.Protocol.Http.UnitLibrary, Curl.Protocol.Http.UnitTests]
requirement: none
created: 2026-10-08
completed: 2026-10-07
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

- [x] The finding's reproduction, run from the repository root, gives the expected result, not the actual one it recorded.
- [x] `dotnet build` and the fast tests (`dotnet test --filter "TestCategory!=Integration"`) pass.

## Notes

- The production code was right; only a test was missing. The mutant `>` is invisible to
  any line that ends: a 4096-byte line reaches `EndTrailerLine`, whose own check
  (`Length + 2 >= 4096`) fails it with the same exit 100. It shows only when the
  connection closes after exactly 4096 trailer bytes with no line end: the real check
  fails at the 4096th byte with exit 100, the mutant waits for a 4097th and the close
  gives exit 18.
- New test `HttpChunkedDecoderTests.CopyAsync_UnendedTrailerThenClose_FailsTooLargeOnlyAtThe4096thByte`
  pins both sides: 4095 bytes then close is exit 18, 4096 bytes then close is exit 100,
  at every chunk size.
- Added `Curl.Protocol.Http.UnitTests` to `touches`: the fix is a test there. No task in
  Doing on `origin/work/dark-factory` names it.
- `Audit/Tools/Invoke-MutationTest.ps1` is an audit path a lane may not run, so the
  mutant was applied by hand instead (`>=` -> `>` on line 221): the new "4096 bytes,
  closed" case failed (killed), and passes again with the original line. The quality
  auditor's re-audit runs the script itself.
- Fast tests: Curl.Protocol.Http.UnitTests 1886 passed, 18 skipped; whole fast run green.

## Log

- 2026-10-08: Created.
- 2026-10-07: Backlog -> Doing.
- 2026-10-07: Doing -> Done. A trailer line that reaches 4096 bytes before the connection closes now has a test pinning exit 100, killing AF-0060's mutant
