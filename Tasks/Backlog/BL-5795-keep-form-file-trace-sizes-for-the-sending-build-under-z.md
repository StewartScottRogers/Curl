---
id: BL-5795
title: Keep form-file trace sizes for the sending build under -Z
priority: Normal
assignee: Claude
pipeline: direct
depends-on: []
touches: [Curl.Console/CurlCommandRunner.cs, Curl.Core.UnitLibrary/Multipart/MultipartFormBodyBuilder.cs, Curl.Console.UnitTests/FormFileSizedBeforeTraceTests.cs]
requirement: none
created: 2026-11-22
completed:
---
# BL-5795 — Keep form-file trace sizes for the sending build under -Z

## Goal

Under `-Z`, a `-F` file that the `--trace`/`--trace-ascii` file truncates still makes the transfer fail with exit 26, as it already does without `-Z` (AF-0982, BL-5784).

## Context

Found by the shift-end review of 2026-11-22 (type-design-analyzer, confirmed against the code).

BL-5784 added `MultipartFormBodyBuilder.FileSizesBeforeTrace`. `CurlCommandRunner.SizeFormFilesBeforeTraceFileOpensAsync` (`Curl.Console/CurlCommandRunner.cs:1875`) sets it before the trace file opens. The `finally` in `BuildFormAsync` (`CurlCommandRunner.cs:4718-4721`) clears it after the first form build.

Under `-Z`, `HoldInFirstBatchAsync` first calls `FormSetupFailureAsync` (`CurlCommandRunner.cs:2252`). That method builds the form only to check it (`:2340`) and throws the body away, and the `finally` clears the sizes at that point. The build that actually sends the request (`TransferWithFormAsync`, `:4610`) then sees no sizes.

Example: `curl -Z -F f=@f.txt --trace-ascii f.txt http://host/` declares the truncated length and does not end with exit 26. The serial path is right. No test in `FormFileSizedBeforeTraceTests.cs` uses `-Z`.

Suggested fix: stop sharing mutable state on the builder. Pass the sizes into `BuildAsync`/`BuildMailMessageAsync` as a parameter, and have the runner hand them only to the build that sends. This also removes two other problems:
- the sizes carrying over to a later `--next` group when the first group never builds a form;
- the race between parallel builds.

## Acceptance criteria

- [ ] A test in `Curl.Console.UnitTests` runs `-Z -F f=@<file> --trace-ascii <same file>` and asserts exit 26, the same as the serial test.
- [ ] `FormSetupFailureAsync`'s check build no longer uses up or clears the sizes that the sending build reads.
- [ ] The existing `FormFileSizedBeforeTraceTests` and `MultipartFormBodyBuilderTraceSizeTests` still pass.
- [ ] `Curl.Console` and `Curl.Core.UnitLibrary` keep 100% line and branch coverage.

## Notes

## Log

- 2026-11-22: Created.
