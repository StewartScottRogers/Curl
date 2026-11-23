---
id: BL-5800
title: Hand form-file trace sizes only to the first sending build under -Z
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Console.UnitTests/FormFileSizedBeforeTraceTests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5800 — Hand form-file trace sizes only to the first sending build under -Z

## Goal

Under `-Z`, whether a form transfer reads the `-F` file sizes taken before the trace file opened no longer depends on which parallel form build reaches its `finally` first: the same command line gives the same exit codes on every run.

## Context

Found by the shift-end review of 2026-11-22 (type-design-analyzer and silent-failure-hunter lenses, checked against the code) in the BL-5795 change.

- `CurlCommandRunner.SizeFormFilesBeforeTraceFileOpensAsync` (`Curl.Console/CurlCommandRunner.cs:1875`) stores the sizes in the shared, mutable `MultipartFormBodyBuilder.FileSizesBeforeTrace` (`Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs:99`), read by `SizeBeforeTrace` (`:421`).
- BL-5795 made `FormSetupFailureAsync`'s check build (`CurlCommandRunner.cs:2340`) pass `isSendingBuild: false`, so the sizes now survive until the first *sending* build's `finally` (`CurlCommandRunner.cs:4720-4725`) clears them.
- Before BL-5795 the check build cleared them before any `-Z` transfer started, so every parallel sending build saw none (wrong, but deterministic). Now, with two or more form transfers in one `-Z` batch, every sending build that reaches `SizeBeforeTrace` before the first one's `finally` runs reads the sizes, and the rest do not. Sending builds run concurrently from `StartNowInParallelAsync` and await file opens, so which transfers declare the pre-trace length (and end with exit 26) varies from run to run.
- Example: `curl -Z -F f=@f.txt --trace-ascii f.txt http://a/ http://b/`. BL-5795's notes already name this race as not addressed; no task covered it.

Suggested fix (from BL-5795's own context): stop sharing mutable state on the builder. Pass the sizes into `BuildAsync`/`BuildMailMessageAsync` as a parameter and have the runner hand them, atomically, to exactly one sending build (for example by `Interlocked.Exchange` on a runner field), which also removes the `--next` carry-over.

## Acceptance criteria

- [x] `MultipartFormBodyBuilder` no longer holds the pre-trace sizes as a settable property; the build that uses them receives them as an argument.
- [x] Under `-Z`, exactly one sending form build per run receives the pre-trace sizes, taken atomically, and the check build in `FormSetupFailureAsync` receives none.
- [x] A test in `Curl.Console.UnitTests` runs `-Z -F f=@f.txt --trace-ascii f.txt` with two URLs and asserts the same per-transfer outcome on every run (a handler that holds the first build until the second has read its sizes shows the race is gone).
- [x] `RunAsync_ParallelTraceTruncatesTheFormFile_EndsWithExit26` and the other `FormFileSizedBeforeTraceTests` and `MultipartFormBodyBuilderTraceSizeTests` still pass.
- [x] `Curl.Console` and `Curl.Core.UnitLibrary` keep 100% line and branch coverage.

## Notes

- The sizes now live in a runner field (`formFileSizesBeforeTrace`); `BuildFormAsync` takes them with `Interlocked.Exchange` for a sending build only, so exactly one sending build gets them and the `FormSetupFailureAsync` check build and any `--next` carry-over get none. `MultipartFormBodyBuilder` takes them as an optional `fileSizesBeforeTrace` argument of `BuildAsync` and `BuildMailMessageAsync`; the settable property is gone.
- New test uses `--parallel-immediate` so both URLs start together (without it the second waits for the first). Measure-CodeQuality for Curl.Core.UnitLibrary and Curl.Console: 0 failing members. Fast tests green. The Linux and macOS criterion is met by keeping the change platform-neutral.

## Log

- 2026-11-22: Created.
- 2026-11-22: Backlog -> Doing. Criteria at claim: 4f547ad5 3cd9a84e c458d85a e0a08499 4717605e.
